using DfoClient;
using Microsoft.Extensions.Logging;
using ToaArchiver.Domain.Core;
using ToaArchiver.Domain.Messages;

namespace ToaArchiver.Domain;

public class ContractSignedHandler : MessageHandlerBase<ContractSignedMessage>
{
    private readonly IArchive _archive;
    private readonly IClient _dfoClient;

    public ContractSignedHandler(IArchive archive, IClient dfoClient, ContractSignedMessage message, ILogger logger) : base(message, logger)
    {
        _archive = archive;
        _dfoClient = dfoClient;
    }

    protected override void Execute(ContractSignedMessage message)
    {
        throw new NotImplementedException("This message handler is based on async logic.");
    }

    protected override async Task ExecuteAsync(ContractSignedMessage message)
    {
        var employee = await _dfoClient.GetEmployeeAsync(message.EmployeeId, message.ValidAfter);
        if (employee == null) return;

        var contract = await _dfoClient.GetContractAsync(sequenceNumber: message.SequenceNumber, employeeId: null, date: null);

        DTO.SignedContractData uploadFileRequirements = new DTO.SignedContractData
        {
            SequenceNumber = message.SequenceNumber,
            SocialSecurityNumber = employee.SocialSecurityNumber,
            FirstName = employee.FirstName,
            Surname = employee.LastName,
            StreetAddress = employee.Address,
            PostalCode = employee.Zipcode,
            PostalOffice = employee.City,
            MobilePhoneNumber = employee.PhoneNumber,
            Email = employee.Email,
            FileContent = contract.FileContent
        };
        await _archive.UploadSignedContractAsync(uploadFileRequirements);
    }

    protected override bool HandlesMessages() => true;
}
