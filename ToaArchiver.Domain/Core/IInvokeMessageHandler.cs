namespace ToaArchiver.Domain.Core;

public interface IInvokeMessageHandler
{
    void Invoke(string message);
    Task InvokeAsync(string message);
}
