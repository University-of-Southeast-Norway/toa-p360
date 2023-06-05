namespace ToaArchiver.Domain.Messages;

public record class ContractStatusChangedMessage(string SequenceNumber = default!, string CorrelationId = default!, string Source = default!, string Subject = default!, string Uri = default!, DateTimeOffset ValidAfter = default)
    : MessageBase(SequenceNumber, CorrelationId, Source, Subject, Uri, ValidAfter);
