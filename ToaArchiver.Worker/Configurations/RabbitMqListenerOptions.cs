namespace ToaArchiver.Worker.Configurations
{
    public class RabbitMqListenerOptions
    {
        public string Scheme { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string VirtualHost { get; set; }
        public string Queue { get; set; }
        public bool AckAllMessages { get; set; }
        public bool AckHandledMessages { get; set; }
        public int MaxSimultaneousMessages { get; set; }
        public RetryOptions Retry { get; set; } = new();
    }

    public enum RetryMode
    {
        Requeue,
        Broker
    }

    public class RetryOptions
    {
        public int InProcessAttempts { get; set; } = 3;
        public TimeSpan InProcessDelay { get; set; } = TimeSpan.FromSeconds(2);
        public RetryMode Mode { get; set; } = RetryMode.Requeue;
        public TimeSpan[]? Delays { get; set; }
        public int MaxAttempts { get; set; } = 10;
    }
}
