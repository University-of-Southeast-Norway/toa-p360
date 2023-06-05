using ToaArchiver.Domain.Core.Generic;

namespace ToaArchiver.Domain.Core;

public class StringMessageHandlerInvoker : IInvokeMessageHandler
{
    private readonly IParseMessage _messageParser;

    public StringMessageHandlerInvoker(IParseMessage messageParser)
    {
        _messageParser = messageParser;
    }

    public void Invoke(string message)
    {
        var messageHandler = _messageParser.Parse(message);
        messageHandler.Execute();
    }

    public async Task InvokeAsync(string message)
    {
        var messageHandler = _messageParser.Parse(message);
        await messageHandler.ExecuteAsync();
    }
}
