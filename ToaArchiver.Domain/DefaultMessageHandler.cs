using Microsoft.Extensions.Logging;
using ToaArchiver.Domain.Core;

namespace ToaArchiver.Domain;

public class DefaultMessageHandler : MessageHandlerBase<string>
{
    private string message;

    public DefaultMessageHandler(string message, ILoggerFactory loggerFactory) : base(message, loggerFactory)
    {
        this.message = message;
    }

    protected override void Execute(string message)
    {
    }

    protected override Task ExecuteAsync(string message)
    {
        return Task.CompletedTask;
    }

    protected override bool HandlesMessages() => false;
}
