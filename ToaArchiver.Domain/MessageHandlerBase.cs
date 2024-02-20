using Microsoft.Extensions.Logging;
using ToaArchiver.Domain.Core;

namespace ToaArchiver.Domain;

public abstract class MessageHandlerBase<T> : IHandleMessage
{
    private readonly T _message;
    private readonly ILogger<MessageHandlerBase<T>> _logger;

    public bool Handled => HandlesMessages();

    protected abstract bool HandlesMessages();

    protected MessageHandlerBase(T message, ILoggerFactory loggerFactory)
    {
        _message = message;
        _logger = loggerFactory.CreateLogger<MessageHandlerBase<T>>();
    }

    public void Execute()
    {
        _logger.LogInformation("Executing messagehandler {HandlerType}", GetType());
        _logger.LogDebug("...on message {Message}", _message);
        Execute(_message);
    }
    protected abstract void Execute(T message);

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Executing messagehandler {HandlerType}", GetType());
        _logger.LogDebug("...on message {Message}", _message);
        await ExecuteAsync(_message);
    }
    protected abstract Task ExecuteAsync(T message);
}