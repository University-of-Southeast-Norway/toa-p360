using DfoClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToaArchiver.Domain.Core;
using ToaArchiver.Domain.Extensions;
using ToaArchiver.Domain.Messages;

namespace ToaArchiver.Domain;

public class ContractStatusChangedHandler : MessageHandlerBase<ContractStatusChangedMessage>
{
    private readonly IArchive _archive;
    private readonly IClient _dfoClient;
    private readonly ILogger<ContractStatusChangedHandler> _logger;
    private readonly IOptionsMonitor<ToaOptions> _options;

    public ContractStatusChangedHandler(IArchive archive, IClient dfoClient, ContractStatusChangedMessage contractChangedMessage, IOptionsMonitor<ToaOptions> options, ILoggerFactory loggerFactory) : base(contractChangedMessage, loggerFactory)
    {
        _archive = archive;
        _dfoClient = dfoClient;
        _options = options;
        _logger = loggerFactory.CreateLogger<ContractStatusChangedHandler>();
    }

    protected override void Execute(ContractStatusChangedMessage contractChangedMessage)
    {
        throw new NotImplementedException("This message handler is based on async logic.");
    }

    protected override async Task ExecuteAsync(ContractStatusChangedMessage contractChangedMessage)
    {
        string sequenceNumber = contractChangedMessage.SequenceNumber;
        Contract contract = await _dfoClient.GetContractAsync(sequenceNumber);

        if (contract == null)
        {
            _logger.LogWarning("Could not fetch contract on sequence number {ContractSequenceNumber}", sequenceNumber);
            return;
        }

        _logger.LogInformation("Contract with sequence number {ContractSequenceNumber} has status {ContractStatus}", sequenceNumber, contract.Status);
        if (!IsContractSigned(contract))
        {
            _logger.LogInformation("Contract on sequence number {ContractSequenceNumber} is not signed", sequenceNumber);
            return;
        }

        DateTimeOffset searchDate = contractChangedMessage.ValidAfter > DateTimeOffset.Now.Date ? DateTimeOffset.Now.Date : contractChangedMessage.ValidAfter;
        Employee employee = await _dfoClient.GetEmployeeAsync(contract.EmployeeId, searchDate);
        if (employee == null)
        {
            _logger.LogWarning("Could not fetch employee on employee-ID {EmployeeId}", contract.EmployeeId);
            return;
        }

        Employee? caseManager = null;
        EmployeeContract? employeeContract = await _dfoClient.GetEmployeeContractAsync(employee.Id, contract.ContractId);
        if (employeeContract == null)
        {
            _logger.LogWarning("Could not fetch employee contract on employee-ID {EmployeeId} with contract-ID {ContractId}, will not add case manager", contract.EmployeeId, contract.ContractId);
        }
        else caseManager = (await _dfoClient.QueryEmployeeAsync(dfoBrukerident: employeeContract.CaseManager))?.FirstOrDefault();

        DTO.SignedContractData uploadFileRequirements = new()
        {
            SequenceNumber = sequenceNumber,
            SocialSecurityNumber = employee.SocialSecurityNumber,
            FirstName = employee.FirstName,
            Surname = employee.LastName,
            StreetAddress = employee.Address,
            PostalCode = employee.Zipcode,
            PostalOffice = employee.City,
            MobilePhoneNumber = employee.PhoneNumber,
            Email = employee.Email,
            FileContent = contract.FileContent,
            SignedDate = contract.Date,
            CaseManagerId = caseManager?.Id,
            CaseManagerEmail = caseManager?.Email
        };
        await _archive.UploadSignedContractAsync(uploadFileRequirements);
    }

    private bool IsContractSigned(Contract contract)
    {
        return contract.Status?.ContainsAny(_options.CurrentValue.ContractSignedStatuses!) == true;
    }

    protected override bool HandlesMessages() => true;
}