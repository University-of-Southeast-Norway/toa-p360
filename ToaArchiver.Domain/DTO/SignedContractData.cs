namespace ToaArchiver.Domain.DTO
{
    public record class SignedContractData
    {
        public string? SocialSecurityNumber { get; init; }
        public string? FirstName { get; init; }
        public string? MiddleName { get; init; }
        public string? Surname { get; init; }
        public string? StreetAddress { get; init; }
        public string? PostalCode { get; init; }
        public string? PostalOffice { get; init; }
        public string? MobilePhoneNumber { get; init; }
        public string? Email { get; init; }
        public string? SequenceNumber { get; init; }
        public string? FileContent { get; init; }
    }
}
