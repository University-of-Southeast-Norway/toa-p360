using DfoClient;
using Moq;
using Serilog;
using Serilog.Extensions.Logging;
using System.Dynamic;
using System.Net.NetworkInformation;
using ToaArchiver.Domain.Core;
using ToaArchiver.Domain.Messages;
using Xunit.Abstractions;

namespace ToaArchiver.Domain.UnitTest;

public class ContractSignedHandlerTester
{
    private readonly Mock<IClient> _mockDfoClient = new();
    private readonly Mock<IArchive> _mockArchive = new();
    private readonly ContractSignedHandler _contractSignedHandler;
    private const string MessageId = "MessageId";
    private const string EmployeeId = "EmployeeId";
    private const string Id = "Id";
    private const string Subject = "Subject";
    private const string Source = "Source";
    private const string CorrelationId = "CorrelationId";
    private const string Uri = "Uri";
    private readonly Mock<ITestOutputHelper> _mockTestOutputHelper;

    public ContractSignedHandlerTester()
    {
        ContractSignedMessage message = new(MessageId, EmployeeId, Id, CorrelationId, Source, Subject, Uri);
        _mockTestOutputHelper = MockLogger();
        Microsoft.Extensions.Logging.ILogger microsoftLogger = new SerilogLoggerFactory(ConfigureLogger(_mockTestOutputHelper.Object)).CreateLogger(nameof(ContractSignedHandlerTester));
        _contractSignedHandler = new (_mockArchive.Object, _mockDfoClient.Object, message, microsoftLogger);
    }

    [Fact]
    public async Task ExecuteAsync_called__Calls_UploadSignedContract()
    {
        // Given
        Employee employee = CreateEmployeeStub();
        MockGetEmployee(employee);
        Contract contract = CreateContractStub();
        MockGetContract(contract);
        DTO.SignedContractData signedContractData = null;
        _mockArchive.Setup(a => a.UploadSignedContractAsync(It.IsAny<DTO.SignedContractData>())).Callback<DTO.SignedContractData>(s => signedContractData = s);


        // When
        await _contractSignedHandler.ExecuteAsync();

        // Then
        _mockArchive.Verify(a => a.UploadSignedContractAsync(It.IsAny<DTO.SignedContractData>()), Times.Once);
        Assert.Equal(employee.SocialSecurityNumber, signedContractData.SocialSecurityNumber);
        Assert.Equal(employee.FirstName, signedContractData.FirstName);
        Assert.Equal(employee.LastName, signedContractData.Surname);
        Assert.Equal(employee.Address, signedContractData.StreetAddress);
        Assert.Equal(employee.Zipcode, signedContractData.PostalCode);
        Assert.Equal(employee.City, signedContractData.PostalOffice);
        Assert.Equal(employee.PhoneNumber, signedContractData.MobilePhoneNumber);
        Assert.Equal(employee.Email, signedContractData.Email);
    }

    [Fact]
    public void ExecuteCalled__Logs_message_and_throws_exception()
    {
        // Given
        MockGetEmployee(CreateEmployeeStub());
        MockGetContract(CreateContractStub());

        // When
        Assert.ThrowsAsync<Exception>(_contractSignedHandler.ExecuteAsync);

        // Then
        _mockTestOutputHelper.Verify(t => t.WriteLine(It.Is<string>(s => s.Contains(" INF] Executing messagehandler ToaArchiver.Domain.ContractSignedHandler"))), Times.Once);
        _mockTestOutputHelper.Verify(t => t.WriteLine(It.Is<string>(s => s.Contains(@" DBG] ...on message {""SequenceNumber"":""MessageId"",""EmployeeId"":""EmployeeId"",""Id"":""Id"",""CorrelationId"":""CorrelationId"",""Source"":""Source"",""Subject"":""Subject"",""Uri"":""Uri"",""ValidAfter"":""0001-01-01T00:00:00.0000000+00:00"",""RawData"":null,""JsonData"":null,""$type"":""ContractSignedMessage""}"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsyncCalled__Logs_message()
    {
        // Given
        MockGetEmployee(CreateEmployeeStub());
        MockGetContract(CreateContractStub());

        // When
        await _contractSignedHandler.ExecuteAsync();

        // Then
        _mockTestOutputHelper.Verify(t => t.WriteLine(It.Is<string>(s => s.Contains(" INF] Executing messagehandler ToaArchiver.Domain.ContractSignedHandler"))), Times.Once);
        _mockTestOutputHelper.Verify(t => t.WriteLine(It.Is<string>(s => s.Contains(@" DBG] ...on message {""SequenceNumber"":""MessageId"",""EmployeeId"":""EmployeeId"",""Id"":""Id"",""CorrelationId"":""CorrelationId"",""Source"":""Source"",""Subject"":""Subject"",""Uri"":""Uri"",""ValidAfter"":""0001-01-01T00:00:00.0000000+00:00"",""RawData"":null,""JsonData"":null,""$type"":""ContractSignedMessage""}"))), Times.Once);
    }

    private void MockGetEmployee(Employee employee)
    {
        _mockDfoClient.Setup(d => d.GetEmployeeAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>())).Returns(Task.FromResult(employee));
    }

    private static Employee CreateEmployeeStub()
    {
        dynamic dynamicEmployee = new ExpandoObject();
        dynamicEmployee.fnr = "fnr";
        dynamicEmployee.fornavn = "fornavn";
        dynamicEmployee.etternavn = "etternavn";
        dynamicEmployee.privatPostadresse = "privatPostadresse";
        dynamicEmployee.privatPostnr = "privatPostnr";
        dynamicEmployee.privatPoststed = "privatPoststed";
        dynamicEmployee.mobilPrivat = "mobilPrivat";
        dynamicEmployee.epost = "epost";
        return new Employee(dynamicEmployee);
    }

    private void MockGetContract(Contract contract)
    {
        _mockDfoClient.Setup(d => d.GetContractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>())).Returns(Task.FromResult(contract));
    }

    private Contract CreateContractStub()
    {
        dynamic contractData = new ExpandoObject();

        contractData.sekvensnr = "sekvensnr";
        contractData.kontraktnr = "kontraktnr";
        contractData.ansattId = "ansattId";
        contractData.status = "status";
        contractData.kostnadssted = "kostnadssted";
        contractData.organisasjonsId = 0;
        contractData.firmakode = "firmakode";
        contractData.dato = (DateTimeOffset)default;
        contractData.tidspunkt = (TimeSpan)default;
        contractData.filtype = "filtype";
        contractData.filinnhold = "filinnhold";
        return new Contract(contractData);
    }

    private Mock<ITestOutputHelper> MockLogger()
    {
        Mock<ITestOutputHelper> mockTestOutputHelper = new Mock<ITestOutputHelper>();
        return mockTestOutputHelper;
    }

    private static ILogger ConfigureLogger(ITestOutputHelper testOutputHelper)
    {
        return new LoggerConfiguration().MinimumLevel.Debug().WriteTo.TestOutput(testOutputHelper).CreateLogger();
    }
}
