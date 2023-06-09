﻿using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using ToaArchiver.Domain.Core.Generic;
using ToaArchiver.Domain.Core;
using Microsoft.Extensions.Options;
using ToaArchiver.Worker.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ToaArchiver.Worker
{
    public class RabbitMqListenerService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IConnection? _connection;
        private readonly IOptionsMonitor<RabbitMqListenerOptions> _options;
        private readonly ILogger<RabbitMqListenerService> _logger;
        private IModel? _model;
        private EventingBasicConsumer? _consumer;
        private static readonly SemaphoreSlim _theLock = new(1,1);

        public RabbitMqListenerService(IServiceScopeFactory serviceScopeFactory, IConnection connection, IOptionsMonitor<RabbitMqListenerOptions> options, ILogger<RabbitMqListenerService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _connection = connection;
            _logger = logger;
            _options = options;
        }

        public RabbitMqListenerService(IServiceScopeFactory serviceScopeFactory, ConnectionFactory connectionFactory, IOptionsMonitor<RabbitMqListenerOptions> options, ILogger<RabbitMqListenerService> logger)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _options = options;
            _connection = CreateConnection(connectionFactory);
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
            _logger.LogInformation("Message received with delivery tag {DeliveryTag}", message.DeliveryTag);
            _logger.LogDebug("Message received {@Message}", message);
            try
            {
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
                _model?.BasicNack(message.DeliveryTag, false, true);
                _logger.LogError(ex, "Unhandled exception occured while trying to handle message {Message}", message);
            }
            finally
            {
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