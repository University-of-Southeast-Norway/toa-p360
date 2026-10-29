using ToaArchiver.Worker;
using ToaArchiver.Worker.Configurations;

namespace ToaArchiver.UnitTest;

public class ConsumerCircuitBreakerTester
{
    private readonly ManualTimeProvider _timeProvider = new();
    private readonly ConsumerCircuitBreaker _circuitBreaker;

    public ConsumerCircuitBreakerTester()
    {
        _circuitBreaker = new(new CircuitBreakerOptions { FailureThreshold = 3, BreakDuration = TimeSpan.FromMinutes(5) }, _timeProvider);
    }

    [Fact]
    public void RecordFailure_called__Threshold_reached__Opens_circuit_once()
    {
        Assert.False(_circuitBreaker.RecordFailure());
        Assert.False(_circuitBreaker.RecordFailure());
        Assert.True(_circuitBreaker.RecordFailure());
        Assert.False(_circuitBreaker.RecordFailure());
        Assert.Equal(CircuitState.Open, _circuitBreaker.State);
    }

    [Fact]
    public void RecordSuccess_called__Between_failures__Resets_count()
    {
        _circuitBreaker.RecordFailure();
        _circuitBreaker.RecordFailure();
        _circuitBreaker.RecordSuccess();

        Assert.False(_circuitBreaker.RecordFailure());
        Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
    }

    [Fact]
    public void TryHalfOpen_called__Before_and_after_break_duration__Half_opens_after()
    {
        Open();

        _timeProvider.Advance(TimeSpan.FromMinutes(4));
        Assert.False(_circuitBreaker.TryHalfOpen());
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        Assert.True(_circuitBreaker.TryHalfOpen());
        Assert.Equal(CircuitState.HalfOpen, _circuitBreaker.State);
    }

    [Fact]
    public void RecordFailure_called__When_half_open__Reopens_on_first_failure()
    {
        Open();
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _circuitBreaker.TryHalfOpen();

        Assert.True(_circuitBreaker.RecordFailure());
        Assert.Equal(CircuitState.Open, _circuitBreaker.State);
    }

    [Fact]
    public void RecordSuccess_called__When_half_open__Closes_circuit()
    {
        Open();
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _circuitBreaker.TryHalfOpen();

        Assert.True(_circuitBreaker.RecordSuccess());
        Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
    }

    [Fact]
    public void RecordFailure_called__Threshold_zero__Never_opens()
    {
        ConsumerCircuitBreaker disabled = new(new CircuitBreakerOptions { FailureThreshold = 0 }, _timeProvider);

        for (int i = 0; i < 10; i++) Assert.False(disabled.RecordFailure());
        Assert.Equal(CircuitState.Closed, disabled.State);
    }

    private void Open()
    {
        for (int i = 0; i < 3; i++) _circuitBreaker.RecordFailure();
    }

    private class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
