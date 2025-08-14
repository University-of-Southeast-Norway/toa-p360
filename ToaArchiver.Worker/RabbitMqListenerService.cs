using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using ToaArchiver.Domain.Core.Generic;
using ToaArchiver.Domain.Core;
using Microsoft.Extensions.Options;
using ToaArchiver.Worker.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace ToaArchiver.Worker
{
    public class RabbitMqListenerService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IConnection? _connection;
        private readonly IOptionsMonitor<RabbitMqListenerOptions> _options;
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<RabbitMqListenerService> _logger;
        private IModel? _model;
        private EventingBasicConsumer? _consumer;
        private static readonly SemaphoreSlim _theLock = new(1,1);
        private static int _currentCount = 0;

        public RabbitMqListenerService(IServiceScopeFactory serviceScopeFactory, IConnection connection, IOptionsMonitor<RabbitMqListenerOptions> options, ILogger<RabbitMqListenerService> logger, TelemetryClient telemetryClient)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _connection = connection;
            _logger = logger;
            _options = options;
            _telemetryClient = telemetryClient;
        }

        public RabbitMqListenerService(IServiceScopeFactory serviceScopeFactory, ConnectionFactory connectionFactory, IOptionsMonitor<RabbitMqListenerOptions> options, ILogger<RabbitMqListenerService> logger, TelemetryClient telemetryClient)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _options = options;
            _connection = CreateConnection(connectionFactory);
            _telemetryClient = telemetryClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _model = InitModel();
            _consumer = new EventingBasicConsumer(_model);
            _consumer.Received += (model, ea) =>
            {
                MessageReceived(ea);
            };
            _model.BasicConsume(queue: _options.CurrentValue.Queue, autoAck: false, consumer: _consumer);
            _logger.LogInformation("Listening on {Queue}@{Connection}", _options.CurrentValue.Queue, _connection);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(10000, stoppingToken);
            }
        }

        private IModel InitModel()
        {
            return _connection?.CreateModel()!;
        }

        private async void MessageReceived(BasicDeliverEventArgs message)
        {
            if (_currentCount >= _options.CurrentValue.MaxSimultaneousMessages)
            {
                _model?.BasicNack(message.DeliveryTag, false, true);
                return;
            }
            RequestTelemetry requestTelemetry = new() { Name = $"Process {_connection?.Endpoint}" };
            requestTelemetry.Context.Operation.Id = message.BasicProperties.MessageId;
            requestTelemetry.Context.Operation.ParentId = message.BasicProperties.CorrelationId;
            using var operation = _telemetryClient.StartOperation(requestTelemetry);
            _telemetryClient.TrackEvent("Message received", new Dictionary<string, string> {  { "RoutingKey", message.RoutingKey  } });
            _logger.LogInformation("Message received with routing key {RoutingKey}", message.RoutingKey);
            _logger.LogDebug("Message redelivered: {Redelivered}", message.Redelivered);
            _logger.LogDebug("Message redelivered: {MessageId}", message.BasicProperties.MessageId);
            _logger.LogDebug("Message redelivered: {CorrelationId}", message.BasicProperties.CorrelationId);

            try
            {
                _currentCount++;
                byte[] body = message.Body.ToArray();
                await _theLock.WaitAsync();

                IServiceScope serviceScope = _serviceScopeFactory.CreateScope();
                var messageHandlerInvoker = serviceScope.ServiceProvider.GetRequiredService<IInvokeMessageHandler<byte[]>>();
                IHandleMessage messageHandler = await messageHandlerInvoker.InvokeAsync(body);
                RabbitMqListenerOptions currentOptions = _options.CurrentValue;
                if (currentOptions.AckAllMessages || currentOptions.AckHandledMessages && messageHandler.Handled) _model?.BasicAck(message.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                _model?.BasicNack(message.DeliveryTag, false, true);
                _logger.LogError(ex, "Unhandled exception occured while trying to handle message {Message}", message);
            }
            finally
            {
                _currentCount--;
                _theLock.Release();
            }
        }

        private IConnection CreateConnection(ConnectionFactory connectionFactory)
        {
            try
            {
                return connectionFactory.CreateConnection();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Could not connect to RabbitMq-queue");
                throw;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _connection?.Close();
            if (_model != null) _model.Dispose();
            _model = null;
        }
    }
}