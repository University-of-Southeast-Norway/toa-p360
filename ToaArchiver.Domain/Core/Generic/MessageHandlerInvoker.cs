namespace ToaArchiver.Domain.Core.Generic;
public class MessageHandlerInvoker<TMessage> : IInvokeMessageHandler<TMessage>
{
    private readonly IParseMessage<TMessage> _messageParser;

    public MessageHandlerInvoker(IParseMessage<TMessage> messageParser)
    {
        _messageParser = messageParser;
    }

    public IHandleMessage Invoke(TMessage message)
    {
        var messageHandler = _messageParser.Parse(message);
        messageHandler.Execute();
        return messageHandler;
    }

    public async Task<IHandleMessage> InvokeAsync(TMessage message)
    {
        var messageHandler = _messageParser.Parse(message);
        await messageHandler.ExecuteAsync();
        return messageHandler;
    }
}
public class MessageHandlerInvoker<TParser, TMessage> : MessageHandlerInvoker<TMessage> where TParser : IParseMessage<TMessage>, new()
{
    public MessageHandlerInvoker() : base(new TParser())
    {

    }
}