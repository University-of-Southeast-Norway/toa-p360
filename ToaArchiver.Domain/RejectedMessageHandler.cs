using Microsoft.Extensions.Logging;
using ToaArchiver.Domain.Core;

namespace ToaArchiver.Domain;

public class RejectedMessageHandler : MessageHandlerBase<string>
{
    private readonly string _reason;

    public RejectedMessageHandler(string message, string reason, ILoggerFactory loggerFactory) : base(message, loggerFactory)
    {
        _reason = reason;
    }

    protected override void Execute(string message)
    {
    }

    protected override Task<MessageHandlingResult> ExecuteAsync(string message)
    {
        return Task.FromResult(MessageHandlingResult.Reject(_reason));
    }
}
