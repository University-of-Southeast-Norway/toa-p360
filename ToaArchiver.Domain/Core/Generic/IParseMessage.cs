namespace ToaArchiver.Domain.Core.Generic;

public interface IParseMessage<TMessage>
{
    IHandleMessage Parse(TMessage message);
}
