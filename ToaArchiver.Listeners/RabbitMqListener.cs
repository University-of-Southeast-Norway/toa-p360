using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ToaArchiver.Domain.Core.Generic;

namespace ToaArchiver.Domain.Listeners;

[Obsolete("This class is not maintained anymore.")]
public sealed class RabbitMqListener : IDisposable
{
    private readonly IInvokeMessageHandler<BasicDeliverEventArgs> _messageHandlerInvoker;
    private readonly IConnection? _connection;
    private IModel? _model;
    private EventingBasicConsumer? _consumer;
    private readonly ILogger<RabbitMqListener> _logger;

    public RabbitMqListener(IInvokeMessageHandler<BasicDeliverEventArgs> messageHandlerInvoker, ConnectionFactory connectionFactory, ILogger<RabbitMqListener> logger)
    {
        _connection = CreateConnection(connectionFactory);
        _messageHandlerInvoker = messageHandlerInvoker;
        _logger = logger;
    }

    private IConnection CreateConnection(ConnectionFactory connectionFactory)
    {
        try
        {
            return connectionFactory.CreateConnection();
        }
        catch (System.Exception ex)
        {
            _logger.LogCritical(ex, "Could not connect to RabbitMq-queue");
            throw;
        }
    }

    public RabbitMqListener(IInvokeMessageHandler<BasicDeliverEventArgs> messageHandlerInvoker, IConnection connection, ILogger<RabbitMqListener> logger)
    {
        _connection = connection;
        _logger = logger;
        _messageHandlerInvoker = messageHandlerInvoker;
    }

    private IModel InitModel(string queue)
    {
        IModel channel = _connection?.CreateModel()!;
        channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false);

        return channel;
    }
    
    public void Listen(string queue)
    {
        _model = InitModel(queue);
        _consumer = new EventingBasicConsumer(_model);
        _consumer.Received += (model, ea) =>
        {
            MessageReceived(ea);
        };
        _model.BasicConsume(queue: queue, autoAck: false, consumer: _consumer);
        _logger.LogInformation("Listening on {Queue}@{Connection}", queue, _connection);
    }
    
    private async void MessageReceived(BasicDeliverEventArgs message)
    {
        _logger.LogInformation("Message received with delivery tag {DeliveryTag}", message.DeliveryTag);
        _logger.LogDebug("Message received {Message}", message);
        try
        {
            await _messageHandlerInvoker.InvokeAsync(message);
            _model?.BasicAck(message.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _model?.BasicNack(message.DeliveryTag, false, true);
            _logger.LogError(ex, "Unhandled exception occured while trying to handle message {Message}", message);
        }
    }

    public void Dispose()
    {
        _connection?.Close();
        if (_model != null) _model.Dispose();
        _model = null;
    }
}
