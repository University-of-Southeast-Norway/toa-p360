namespace ToaArchiver.Domain.Core;

public interface IParseMessage
{
    IHandleMessage Parse(string message);
}
