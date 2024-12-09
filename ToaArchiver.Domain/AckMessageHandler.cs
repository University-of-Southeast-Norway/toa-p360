using Microsoft.Extensions.Logging;

namespace ToaArchiver.Domain;

public class AckMessageHandler : MessageHandlerBase<string>
{
    private string message;

    public AckMessageHandler(string message, ILoggerFactory loggerFactory) : base(message, loggerFactory)
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

    protected override bool HandlesMessages() => true;
}
