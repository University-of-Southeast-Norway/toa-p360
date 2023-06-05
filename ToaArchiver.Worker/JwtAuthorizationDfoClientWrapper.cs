using DfoClient;
using Microsoft.Extensions.Options;
using ToaArchiver.Worker.Configurations;

namespace ToaArchiver.Worker;

public class JwtAuthorizationDfoClientWrapper : IClient
{
    private Client _client;
    private readonly ITokenResolver _tokenResolver;

    public JwtAuthorizationDfoClientWrapper(IOptionsMonitor<DfoClientOptions> options, ITokenResolver tokenResolver)
    {
        options.OnChange(RefreshClient);
        _client = new JwtAuthorizationClient(options.CurrentValue.BaseAddress, tokenResolver);
        _tokenResolver = tokenResolver;
    }

    private void RefreshClient(DfoClientOptions options, string? stringArgs)
    {
        _client = new JwtAuthorizationClient(options.BaseAddress, _tokenResolver);
    }

    public async Task<IEnumerable<Contract>> GetContractsAsync(string? contractNumber = null, string? employeeId = null, DateTimeOffset? date = null)
    {
        return await _client.GetContractsAsync(contractNumber!, employeeId!, date);
    }

    public async Task<Contract> GetContractAsync(string sequenceNumber, string? contractNumber= null, string? employeeId = null, DateTimeOffset? date = null)
    {
        return await _client.GetContractAsync(sequenceNumber, contractNumber!, employeeId!, date);
    }

    public async Task<List<string>> GetContractSequenceListAsync(DateTimeOffset from, DateTimeOffset to)
    {
        return await _client.GetContractSequenceListAsync(from, to);
    }

    public async Task<Employee> GetEmployeeAsync(string id, DateTimeOffset? searchDate = null)
    {
        return await _client.GetEmployeeAsync(id, searchDate);
    }

    public async Task<IEnumerable<EmployeeContract>> GetEmployeeContractAsync(string employeeId)
    {
        return await _client.GetEmployeeContractAsync(employeeId);
    }

    public async Task<EmployeeContract> GetEmployeeContractAsync(string employeeId, string contractId)
    {
        return await _client.GetEmployeeContractAsync(employeeId, contractId);
    }

    public async Task<IEnumerable<Contract>> GetContractsAsync(string? contractNumber = null, string? employeeId = null, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, bool includeToDate = false)
    {
        return await _client.GetContractsAsync(contractNumber!, employeeId!, fromDate, toDate, includeToDate);
    }
}
