namespace ToaArchiver.Domain.Core.Generic;

public interface IInvokeMessageHandler<TMessage>
{
    IHandleMessage Invoke(TMessage message);
    Task<MessageHandlingResult> InvokeAsync(TMessage message);
}
