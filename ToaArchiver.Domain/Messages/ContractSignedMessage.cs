namespace ToaArchiver.Domain.Messages;

public record class ContractSignedMessage(string SequenceNumber, string EmployeeId, string Id = default!, string CorrelationId = default!, string Source = default!, string Subject = default!, string Uri = default!, DateTimeOffset ValidAfter = default)
    : MessageBase(Id, CorrelationId, Source, Subject, Uri, ValidAfter);
