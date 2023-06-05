namespace ToaArchiver.Domain.Core;

public interface IHandleMessage
{
    void Execute();
    Task ExecuteAsync();
    bool Handled { get; }
}
