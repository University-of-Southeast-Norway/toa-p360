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

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)]
    public void Schedule_called__With_previous_attempts__Sets_parked_at_only_when_parked(int previousAttempt, bool expectParkedAt)
    {
        _scheduler.Schedule(CreateMessage(previousAttempt), "reason");

        Assert.Equal(expectParkedAt, _publishedProperties!.Headers.ContainsKey(BrokerRetryScheduler.ParkedAtHeader));
    }

    [Fact]
    public void GetStatistics_called__With_parked_messages__Returns_counts_and_requeues_oldest()
    {
        DateTimeOffset parkedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        SetupQueueCount("toa.retry.60s", 2);
        SetupQueueCount("toa.retry.600s", 3);
        SetupQueueCount("toa.parking", 4);
        _mockModel.Setup(m => m.BasicGet("toa.parking", false)).Returns(CreateGetResult(7, new Dictionary<string, object> { [BrokerRetryScheduler.ParkedAtHeader] = parkedAt.ToUnixTimeMilliseconds() }));

        RetryQueueStatistics statistics = _scheduler.GetStatistics();

        Assert.Equal(new RetryQueueStatistics(5, 4, parkedAt), statistics);
        _mockModel.Verify(m => m.BasicNack(7, false, true), Times.Once);
    }

    [Fact]
    public void ReplayParked_called__With_parked_messages__Publishes_to_main_queue_without_retry_headers_and_acks()
    {
        SetupQueueCount("toa.parking", 2);
        Dictionary<string, object> headers = new() { [BrokerRetryScheduler.AttemptHeader] = 11, [BrokerRetryScheduler.ReasonHeader] = "reason", [BrokerRetryScheduler.ParkedAtHeader] = 1L, ["other"] = "kept" };
        _mockModel.SetupSequence(m => m.BasicGet("toa.parking", false)).Returns(CreateGetResult(1, headers)).Returns(CreateGetResult(2, headers)).Returns(CreateGetResult(3, headers));

        int replayed = _scheduler.ReplayParked();

        Assert.Equal(2, replayed);
        Assert.Equal(Queue, _publishedTo);
        Assert.Equal(new[] { "other" }, _publishedProperties!.Headers.Keys);
        _mockModel.Verify(m => m.BasicAck(It.IsAny<ulong>(), false), Times.Exactly(2));
        _mockModel.Verify(m => m.BasicAck(3, false), Times.Never);
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

    private void SetupQueueCount(string queue, uint count) =>
        _mockModel.Setup(m => m.QueueDeclarePassive(queue)).Returns(new QueueDeclareOk(queue, count, 0));

    private static BasicGetResult CreateGetResult(ulong deliveryTag, Dictionary<string, object> headers)
    {
        IBasicProperties properties = new Mock<IBasicProperties>().SetupAllProperties().Object;
        properties.Headers = new Dictionary<string, object>(headers);
        return new BasicGetResult(deliveryTag, false, "", "toa.parking", 0, properties, new byte[] { 1 });
    }

    private static BasicDeliverEventArgs CreateMessage(int? previousAttempt)
    {
        IBasicProperties properties = new Mock<IBasicProperties>().SetupAllProperties().Object;
        properties.MessageId = "message-id";
        if (previousAttempt != null) properties.Headers = new Dictionary<string, object> { [BrokerRetryScheduler.AttemptHeader] = previousAttempt.Value };
        return new BasicDeliverEventArgs("tag", 1, false, "", Queue, properties, new byte[] { 1 });
    }
}
