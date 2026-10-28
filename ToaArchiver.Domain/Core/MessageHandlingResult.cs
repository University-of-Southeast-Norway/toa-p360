namespace ToaArchiver.Domain.Core;

public enum MessageHandlingOutcome
{
    Handled,
    Ignored,
    Retry,
    Reject
}

public record class MessageHandlingResult(MessageHandlingOutcome Outcome, string? Reason = null)
{
    public static MessageHandlingResult Handled() => new(MessageHandlingOutcome.Handled);
    public static MessageHandlingResult Ignored() => new(MessageHandlingOutcome.Ignored);
    public static MessageHandlingResult Retry(string reason) => new(MessageHandlingOutcome.Retry, reason);
    public static MessageHandlingResult Reject(string reason) => new(MessageHandlingOutcome.Reject, reason);
}
