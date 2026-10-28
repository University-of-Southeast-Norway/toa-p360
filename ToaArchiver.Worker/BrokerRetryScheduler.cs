using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ToaArchiver.Worker.Configurations;

namespace ToaArchiver.Worker;

public class BrokerRetryScheduler
{
    public const string AttemptHeader = "x-retry-attempt";
    public const string ReasonHeader = "x-retry-reason";
    private static readonly TimeSpan[] DefaultDelays = { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1), TimeSpan.FromHours(6) };

    private readonly IModel _model;
    private readonly string _queue;
    private readonly TimeSpan[] _delays;
    private readonly int _maxAttempts;

    public BrokerRetryScheduler(IModel model, string queue, RetryOptions options)
    {
        _model = model;
        _queue = queue;
        _delays = options.Delays is { Length: > 0 } ? options.Delays : DefaultDelays;
        _maxAttempts = options.MaxAttempts;
    }

    public string ParkingQueue => $"{_queue}.parking";

    public string WaitQueue(TimeSpan delay) => $"{_queue}.retry.{(long)delay.TotalSeconds}s";

    public void DeclareTopology()
    {
        _model.QueueDeclare(ParkingQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        foreach (TimeSpan delay in _delays.Distinct())
        {
            _model.QueueDeclare(WaitQueue(delay), durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
            {
                { "x-message-ttl", (long)delay.TotalMilliseconds },
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", _queue }
            });
        }
    }

    /// <summary>
    /// Publishes a copy of the message to the wait queue for its next attempt, or to the parking queue when
    /// attempts are exhausted. Returns the queue it was published to. The caller acks the original.
    /// </summary>
    public string Schedule(BasicDeliverEventArgs message, string? reason)
    {
        int attempt = GetAttempt(message.BasicProperties) + 1;
        string target = attempt > _maxAttempts ? ParkingQueue : WaitQueue(_delays[Math.Min(attempt, _delays.Length) - 1]);

        IBasicProperties properties = _model.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = message.BasicProperties.MessageId;
        properties.CorrelationId = message.BasicProperties.CorrelationId;
        properties.ContentType = message.BasicProperties.ContentType;
        properties.Headers = new Dictionary<string, object>(message.BasicProperties.Headers ?? new Dictionary<string, object>())
        {
            [AttemptHeader] = attempt,
            [ReasonHeader] = reason ?? ""
        };

        _model.BasicPublish("", target, false, properties, message.Body);
        _model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
        return target;
    }

    public static int GetAttempt(IBasicProperties properties)
    {
        if (properties.Headers == null || !properties.Headers.TryGetValue(AttemptHeader, out object? value)) return 0;
        return value switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out int parsed) => parsed,
            _ => 0
        };
    }
}
