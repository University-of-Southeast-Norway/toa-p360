using Microsoft.Extensions.Logging;
using ToaArchiver.Domain.Core;

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

    protected override Task<MessageHandlingResult> ExecuteAsync(string message)
    {
        return Task.FromResult(MessageHandlingResult.Handled());
    }
}
