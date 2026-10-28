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
using Polly;
using Polly.Retry;

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
        private AsyncEventingBasicConsumer? _consumer;
        private CancellationToken _stoppingToken;
        private BrokerRetryScheduler? _retryScheduler;

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
            _stoppingToken = stoppingToken;
            _model = InitModel();
            _model.BasicQos(prefetchSize: 0, prefetchCount: (ushort)Math.Clamp(_options.CurrentValue.MaxSimultaneousMessages, 0, ushort.MaxValue), global: false);
            if (_options.CurrentValue.Retry.Mode == RetryMode.Broker)
            {
                _model.ConfirmSelect();
                _retryScheduler = new BrokerRetryScheduler(_model, _options.CurrentValue.Queue, _options.CurrentValue.Retry);
                _retryScheduler.DeclareTopology();
                _logger.LogInformation("Broker retry enabled, parking queue {ParkingQueue}", _retryScheduler.ParkingQueue);
            }
            _consumer = new AsyncEventingBasicConsumer(_model);
            _consumer.Received += (model, ea) => MessageReceived(ea);
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

        private async Task MessageReceived(BasicDeliverEventArgs message)
        {
            RequestTelemetry requestTelemetry = new() { Name = $"Process {_connection?.Endpoint}" };
            requestTelemetry.Context.Operation.Id = message.BasicProperties.MessageId;
            requestTelemetry.Context.Operation.ParentId = message.BasicProperties.CorrelationId;
            using var operation = _telemetryClient.StartOperation(requestTelemetry);
            _telemetryClient.TrackEvent("Message received", new Dictionary<string, string> {  { "RoutingKey", message.RoutingKey  } });
            _logger.LogInformation("Message received with routing key {RoutingKey}", message.RoutingKey);
            _logger.LogDebug("Message redelivered: {Redelivered}", message.Redelivered);
            _logger.LogDebug("Message redelivered: {MessageId}", message.BasicProperties.MessageId);
            _logger.LogDebug("Message redelivered: {CorrelationId}", message.BasicProperties.CorrelationId);

            RabbitMqListenerOptions currentOptions = _options.CurrentValue;
            MessageHandlingResult result;
            try
            {
                byte[] body = message.Body.ToArray();
                result = await BuildInProcessRetryPipeline(currentOptions.Retry).ExecuteAsync(async token =>
                {
                    using IServiceScope serviceScope = _serviceScopeFactory.CreateScope();
                    var messageHandlerInvoker = serviceScope.ServiceProvider.GetRequiredService<IInvokeMessageHandler<byte[]>>();
                    return await messageHandlerInvoker.InvokeAsync(body);
                }, _stoppingToken);
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                _logger.LogError(ex, "Unhandled exception occured while trying to handle message {MessageId}", message.BasicProperties.MessageId);
                result = MessageHandlingResult.Retry(ex.Message);
            }

            bool ackEnabled = currentOptions.AckAllMessages || currentOptions.AckHandledMessages;
            switch (result.Outcome)
            {
                case MessageHandlingOutcome.Handled:
                    if (ackEnabled) _model?.BasicAck(message.DeliveryTag, false);
                    break;
                case MessageHandlingOutcome.Ignored:
                    if (currentOptions.AckAllMessages) _model?.BasicAck(message.DeliveryTag, false);
                    break;
                case MessageHandlingOutcome.Reject:
                    _telemetryClient.TrackEvent("Message rejected", new Dictionary<string, string> { { "RoutingKey", message.RoutingKey }, { "Reason", result.Reason ?? "" } });
                    _logger.LogError("Message {MessageId} rejected: {Reason}", message.BasicProperties.MessageId, result.Reason);
                    if (ackEnabled) _model?.BasicAck(message.DeliveryTag, false);
                    break;
                case MessageHandlingOutcome.Retry:
                    _telemetryClient.TrackEvent("Message retry", new Dictionary<string, string> { { "RoutingKey", message.RoutingKey }, { "Reason", result.Reason ?? "" } });
                    if (_retryScheduler != null && ackEnabled && TryScheduleRetry(message, result.Reason)) break;
                    _logger.LogWarning("Message {MessageId} will be requeued: {Reason}", message.BasicProperties.MessageId, result.Reason);
                    _model?.BasicNack(message.DeliveryTag, false, true);
                    break;
            }
        }

        private bool TryScheduleRetry(BasicDeliverEventArgs message, string? reason)
        {
            try
            {
                string target = _retryScheduler!.Schedule(message, reason);
                _model?.BasicAck(message.DeliveryTag, false);
                if (target == _retryScheduler.ParkingQueue)
                {
                    _telemetryClient.TrackEvent("Message parked", new Dictionary<string, string> { { "RoutingKey", message.RoutingKey }, { "Reason", reason ?? "" } });
                    _logger.LogError("Message {MessageId} parked in {ParkingQueue} after {Retries} retries: {Reason}", message.BasicProperties.MessageId, target, BrokerRetryScheduler.GetAttempt(message.BasicProperties), reason);
                }
                else _logger.LogWarning("Message {MessageId} scheduled for retry via {WaitQueue}: {Reason}", message.BasicProperties.MessageId, target, reason);
                return true;
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                _logger.LogError(ex, "Could not schedule retry for message {MessageId}", message.BasicProperties.MessageId);
                return false;
            }
        }

        private ResiliencePipeline BuildInProcessRetryPipeline(RetryOptions options)
        {
            if (options.InProcessAttempts <= 0) return ResiliencePipeline.Empty;
            return new ResiliencePipelineBuilder().AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.InProcessAttempts,
                Delay = options.InProcessDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception, "Handling attempt {Attempt} failed, retrying in {Delay}", args.AttemptNumber + 1, args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            }).Build();
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