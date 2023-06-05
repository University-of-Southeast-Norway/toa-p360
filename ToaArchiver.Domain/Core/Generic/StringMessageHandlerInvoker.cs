namespace ToaArchiver.Domain.Core.Generic;

public class StringMessageHandlerInvoker<TParser> : MessageHandlerInvoker<TParser, string> where TParser : IParseMessage<string>, new()
{
    public StringMessageHandlerInvoker()
    {
    }
}
