using ToaArchiver.Worker.Configurations;

namespace ToaArchiver.Worker;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public class ConsumerCircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;

    public ConsumerCircuitBreaker(CircuitBreakerOptions options, TimeProvider timeProvider)
    {
        _failureThreshold = options.FailureThreshold;
        _breakDuration = options.BreakDuration;
        _timeProvider = timeProvider;
    }

    public CircuitState State { get; private set; } = CircuitState.Closed;

    public bool Enabled => _failureThreshold > 0;

    public bool RecordFailure()
    {
        if (!Enabled) return false;
        lock (_lock)
        {
            if (State == CircuitState.Open) return false;
            _consecutiveFailures++;
            if (State == CircuitState.Closed && _consecutiveFailures < _failureThreshold) return false;
            State = CircuitState.Open;
            _openedAt = _timeProvider.GetUtcNow();
            return true;
        }
    }

    public bool RecordSuccess()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            if (State != CircuitState.HalfOpen) return false;
            State = CircuitState.Closed;
            return true;
        }
    }

    public bool TryHalfOpen()
    {
        lock (_lock)
        {
            if (State != CircuitState.Open || _timeProvider.GetUtcNow() - _openedAt < _breakDuration) return false;
            State = CircuitState.HalfOpen;
            return true;
        }
    }
}
