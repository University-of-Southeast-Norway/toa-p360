using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ToaArchiver.Worker;
using ToaArchiver.Worker.Configurations;

namespace ToaArchiver.UnitTest;

public class BrokerRetrySchedulerTester
{
    private const string Queue = "toa";
    private readonly Mock<IModel> _mockModel = new();
    private readonly BrokerRetryScheduler _scheduler;
    private string? _publishedTo;
    private IBasicProperties? _publishedProperties;

    public BrokerRetrySchedulerTester()
    {
        _mockModel.Setup(m => m.CreateBasicProperties()).Returns(() => new Mock<IBasicProperties>().SetupAllProperties().Object);
        _mockModel.Setup(m => m.BasicPublish(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IBasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()))
            .Callback<string, string, bool, IBasicProperties, ReadOnlyMemory<byte>>((_, routingKey, _, properties, _) => { _publishedTo = routingKey; _publishedProperties = properties; });
        RetryOptions options = new() { Delays = new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10) }, MaxAttempts = 3 };
        _scheduler = new(_mockModel.Object, Queue, options);
    }

    [Theory]
    [InlineData(null, "toa.retry.60s", 1)]
    [InlineData(1, "toa.retry.600s", 2)]
    [InlineData(2, "toa.retry.600s", 3)]
    [InlineData(3, "toa.parking", 4)]
    public void Schedule_called__With_previous_attempts__Publishes_to_expected_queue(int? previousAttempt, string expectedQueue, int expectedAttempt)
    {
        BasicDeliverEventArgs message = CreateMessage(previousAttempt);

        string target = _scheduler.Schedule(message, "reason");

        Assert.Equal(expectedQueue, target);
        Assert.Equal(expectedQueue, _publishedTo);
        Assert.Equal(expectedAttempt, _publishedProperties!.Headers[BrokerRetryScheduler.AttemptHeader]);
        Assert.Equal("reason", _publishedProperties.Headers[BrokerRetryScheduler.ReasonHeader]);
        Assert.Equal("message-id", _publishedProperties.MessageId);
        _mockModel.Verify(m => m.WaitForConfirmsOrDie(It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public void DeclareTopology_called__Declares_parking_and_wait_queues_dead_lettering_to_main_queue()
    {
        _scheduler.DeclareTopology();

        _mockModel.Verify(m => m.QueueDeclare("toa.parking", true, false, false, null), Times.Once);
        _mockModel.Verify(m => m.QueueDeclare("toa.retry.60s", true, false, false, It.Is<IDictionary<string, object>>(a => (long)a["x-message-ttl"] == 60000 && (string)a["x-dead-letter-routing-key"] == Queue)), Times.Once);
        _mockModel.Verify(m => m.QueueDeclare("toa.retry.600s", true, false, false, It.Is<IDictionary<string, object>>(a => (long)a["x-message-ttl"] == 600000)), Times.Once);
    }

    [Fact]
    public void GetAttempt_called__With_header_as_long__Returns_value()
    {
        IBasicProperties properties = new Mock<IBasicProperties>().SetupAllProperties().Object;
        properties.Headers = new Dictionary<string, object> { [BrokerRetryScheduler.AttemptHeader] = 5L };

        Assert.Equal(5, BrokerRetryScheduler.GetAttempt(properties));
    }

    private static BasicDeliverEventArgs CreateMessage(int? previousAttempt)
    {
        IBasicProperties properties = new Mock<IBasicProperties>().SetupAllProperties().Object;
        properties.MessageId = "message-id";
        if (previousAttempt != null) properties.Headers = new Dictionary<string, object> { [BrokerRetryScheduler.AttemptHeader] = previousAttempt.Value };
        return new BasicDeliverEventArgs("tag", 1, false, "", Queue, properties, new byte[] { 1 });
    }
}
