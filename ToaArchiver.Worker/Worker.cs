using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToaArchiver.Domain.Listeners;
using ToaArchiver.Worker.Configurations;

namespace ToaArchiver.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMqListener _listener;
        private readonly IOptionsMonitor<RabbitMqListenerOptions> _options;

        public Worker(ILogger<Worker> logger, RabbitMqListener listener, IOptionsMonitor<RabbitMqListenerOptions> options)
        {
            _logger = logger;
            _listener = listener;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _listener.Listen(_options.CurrentValue.Queue);
            _logger.LogInformation("Listener {Listener} initialized and listening", _listener);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}