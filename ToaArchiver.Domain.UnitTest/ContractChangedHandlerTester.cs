﻿using DfoClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Dynamic;
using ToaArchiver.Domain.Core;
using ToaArchiver.Domain.Messages;
using Contract = DfoClient.Contract;

namespace ToaArchiver.Domain.UnitTest;

public class ContractChangedHandlerTester
{
    private readonly Mock<IArchive> _mockArchive = new();
    private readonly Mock<IClient> _mockDfoClient = new();
    private readonly Mock<IOptionsMonitor<ToaOptions>> _mockToaOptions = new();
    private readonly ContractStatusChangedHandler _contractStatusChangedHandler;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory = new();
    private const string SequenceNumber = "SequenceNumber";
    private const string Id = "Id";
    private const string Subject = "Subject";
    private const string Source = "Source";
    private const string CorrelationId = "CorrelationId";
    private const string Uri = "Uri";
    private readonly DateTimeOffset ValidAfter = new DateTime(2021,1,1, 0, 0, 0, DateTimeKind.Utc);

    public ContractChangedHandlerTester()
    {
        ContractStatusChangedMessage message = new(SequenceNumber: SequenceNumber, CorrelationId: CorrelationId, Source: Source, Subject: Subject, Uri: Uri, ValidAfter: ValidAfter);
        SetupMockToaOpations();
        _mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        _contractStatusChangedHandler = new(_mockArchive.Object, _mockDfoClient.Object, message, _mockToaOptions.Object, _mockLoggerFactory.Object);
    }

    private void SetupMockToaOpations()
    {
        _mockToaOptions.Setup(t => t.CurrentValue).Returns(new ToaOptions { ContractSignedStatuses = new string[] { "520", "600", "610" }, ProductionDate = new DateTime(2021, 1, 1) });
    }

    [Theory]
    [InlineData("520")]
    [InlineData("600")]
    [InlineData("610")]
    public async Task ExecuteAsync_called__With_status_given__Executes_UploadSignedContractAsync(string status)
    {
        // Given
        Contract contract = CreateContract(status);
        MockGetContract(contract);

        Employee employee = CreateEmployee();
        MockGetEmployee(employee);
        _mockDfoClient.Setup(d => d.GetEmployeeAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>())).Returns(Task.FromResult(employee));

        DTO.SignedContractData signedContractData = null;
        _mockArchive.Setup(a => a.UploadSignedContractAsync(It.IsAny<DTO.SignedContractData>())).Callback<DTO.SignedContractData>(s => signedContractData = s);



        // When
        await _contractStatusChangedHandler.ExecuteAsync();

        // Then
        _mockDfoClient.Verify(d => d.GetContractAsync(SequenceNumber, null, null, null), Times.Once);
        _mockDfoClient.Verify(d => d.GetEmployeeAsync(contract.EmployeeId, ValidAfter), Times.Once);
        _mockArchive.Verify(a => a.UploadSignedContractAsync(It.IsAny<DTO.SignedContractData>()), Times.Once);
        Assert.NotNull(signedContractData);
        Assert.Equal(contract.SequenceNumber, signedContractData.SequenceNumber);
        Assert.Equal(contract.FileContent, signedContractData.FileContent);
        Assert.Equal(employee.SocialSecurityNumber, signedContractData.SocialSecurityNumber);
        Assert.Equal(employee.FirstName, signedContractData.FirstName);
        Assert.Equal(employee.LastName, signedContractData.Surname);
        Assert.Equal(employee.Address, signedContractData.StreetAddress);
        Assert.Equal(employee.Zipcode, signedContractData.PostalCode);
        Assert.Equal(employee.City, signedContractData.PostalOffice);
        Assert.Equal(employee.PhoneNumber, signedContractData.MobilePhoneNumber);
        Assert.Equal(employee.Email, signedContractData.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("400")]
    public async Task ExecuteAsync_called__With_status_given__Does_not_Executes_UploadSignedContractAsync(string status)
    {
        // Given
        Contract contract = CreateContract(status);
        MockGetContract(contract);

        // When
        await _contractStatusChangedHandler.ExecuteAsync();

        // Then
        _mockDfoClient.Verify(d => d.GetContractAsync(SequenceNumber, null, null, null), Times.Once);
        _mockDfoClient.Verify(d => d.GetEmployeeAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
        _mockArchive.Verify(a => a.UploadSignedContractAsync(It.IsAny<DTO.SignedContractData>()), Times.Never);
    }

    private void MockGetEmployeeContract(EmployeeContract employeeContract)
    {
        _mockDfoClient.Setup(d => d.GetEmployeeContractAsync(It.IsAny<string>())).Returns(Task.FromResult<IEnumerable<EmployeeContract>>(new[] { employeeContract }));
    }

    private static EmployeeContract CreateEmployeeContract()
    {
        dynamic dynamicEmployeeContract = new ExpandoObject();
        dynamicEmployeeContract.id = "id";
        dynamicEmployeeContract.kontraktnr = "kontraktnr";
        dynamicEmployeeContract.sekvensnr = "sekvensnr";
        return new EmployeeContract(dynamicEmployeeContract);
    }

    private void MockGetContract(Contract contract)
    {
        _mockDfoClient.Setup(d => d.GetContractAsync(It.IsAny<string>(), null, null, null)).Returns(Task.FromResult(contract));
    }

    private void MockGetContracts(Contract contract)
    {
        _mockDfoClient.Setup(d => d.GetContractsAsync(null, null, null)).Returns(Task.FromResult<IEnumerable<Contract>>(new List<Contract> { contract }));
    }

    private static Contract CreateContract(string status = default!)
    {
        dynamic dynamicContract = new ExpandoObject();
        dynamicContract.kontraktnr = "kontraktnr";
        dynamicContract.sekvensnr = SequenceNumber;
        dynamicContract.status = status;
        dynamicContract.kostnadssted = null;
        dynamicContract.organisasjonsId = null;
        dynamicContract.firmakode = null;
        dynamicContract.dato = null;
        dynamicContract.tidspunkt = null;
        dynamicContract.filtype = null;
        dynamicContract.filinnhold = "filinnhold";
        dynamicContract.ansattId = "ansattId";
        Contract contract = new Contract(dynamicContract);
        return contract;
    }

    private void MockGetEmployee(Employee employee)
    {
        _mockDfoClient.Setup(d => d.GetEmployeeAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>())).Returns(Task.FromResult(employee));
    }

    private static Employee CreateEmployee()
    {
        dynamic dynamicEmployee = new ExpandoObject();
        dynamicEmployee.id = "id";
        dynamicEmployee.fnr = "fnr";
        dynamicEmployee.fornavn = "fornavn";
        dynamicEmployee.etternavn = "etternavn";
        dynamicEmployee.privatPostadresse = "privatPostadresse";
        dynamicEmployee.privatPostnr = "privatPostnr";
        dynamicEmployee.privatPoststed = "privatPoststed";
        dynamicEmployee.mobilPrivat = "mobilPrivat";
        dynamicEmployee.epost = "epost";
        dynamicEmployee.saksbehandler = "saksbehandler";
        dynamicEmployee.organisasjonId = 1;
        dynamicEmployee.stillingId = 2;
        dynamicEmployee.lederflagg = false;
        return new Employee(dynamicEmployee);
    }
}