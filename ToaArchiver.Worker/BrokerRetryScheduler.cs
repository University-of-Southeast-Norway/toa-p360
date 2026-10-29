using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ToaArchiver.Worker.Configurations;

namespace ToaArchiver.Worker;

public class BrokerRetryScheduler
{
    public const string AttemptHeader = "x-retry-attempt";
    public const string ReasonHeader = "x-retry-reason";
    public const string ParkedAtHeader = "x-parked-at";
    private static readonly TimeSpan[] DefaultDelays = { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1), TimeSpan.FromHours(6) };

    private readonly IModel _model;
    private readonly string _queue;
    private readonly TimeSpan[] _delays;
    private readonly int _maxAttempts;
    private readonly TimeProvider _timeProvider;

    public BrokerRetryScheduler(IModel model, string queue, RetryOptions options, TimeProvider? timeProvider = null)
    {
        _model = model;
        _queue = queue;
        _delays = options.Delays is { Length: > 0 } ? options.Delays : DefaultDelays;
        _maxAttempts = options.MaxAttempts;
        _timeProvider = timeProvider ?? TimeProvider.System;
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

        IBasicProperties properties = CopyProperties(message.BasicProperties);
        properties.Headers[AttemptHeader] = attempt;
        properties.Headers[ReasonHeader] = reason ?? "";
        if (target == ParkingQueue) properties.Headers[ParkedAtHeader] = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        _model.BasicPublish("", target, false, properties, message.Body);
        _model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
        return target;
    }

    public RetryQueueStatistics GetStatistics()
    {
        uint waiting = 0;
        foreach (TimeSpan delay in _delays.Distinct()) waiting += _model.QueueDeclarePassive(WaitQueue(delay)).MessageCount;
        uint parked = _model.QueueDeclarePassive(ParkingQueue).MessageCount;

        DateTimeOffset? oldestParkedAt = null;
        BasicGetResult? oldest = parked > 0 ? _model.BasicGet(ParkingQueue, autoAck: false) : null;
        if (oldest != null)
        {
            if (oldest.BasicProperties.Headers?.TryGetValue(ParkedAtHeader, out object? value) == true && value is long parkedAt)
                oldestParkedAt = DateTimeOffset.FromUnixTimeMilliseconds(parkedAt);
            _model.BasicNack(oldest.DeliveryTag, multiple: false, requeue: true);
        }
        return new RetryQueueStatistics(waiting, parked, oldestParkedAt);
    }

    /// <summary>
    /// Moves the messages currently in the parking queue back to the main queue with the retry count reset.
    /// Requires publisher confirms on the model. Returns the number of messages moved.
    /// </summary>
    public int ReplayParked()
    {
        uint count = _model.QueueDeclarePassive(ParkingQueue).MessageCount;
        int replayed = 0;
        while (replayed < count && _model.BasicGet(ParkingQueue, autoAck: false) is { } message)
        {
            IBasicProperties properties = CopyProperties(message.BasicProperties);
            properties.Headers.Remove(AttemptHeader);
            properties.Headers.Remove(ReasonHeader);
            properties.Headers.Remove(ParkedAtHeader);

            _model.BasicPublish("", _queue, false, properties, message.Body);
            _model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
            _model.BasicAck(message.DeliveryTag, multiple: false);
            replayed++;
        }
        return replayed;
    }

    private IBasicProperties CopyProperties(IBasicProperties source)
    {
        IBasicProperties properties = _model.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = source.MessageId;
        properties.CorrelationId = source.CorrelationId;
        properties.ContentType = source.ContentType;
        properties.Headers = new Dictionary<string, object>(source.Headers ?? new Dictionary<string, object>());
        return properties;
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

public record RetryQueueStatistics(uint Waiting, uint Parked, DateTimeOffset? OldestParkedAt);
