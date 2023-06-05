using DfoClient;
using Ks.Fiks.Maskinporten.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;
using ToaArchiver.Worker.Configurations;

namespace ToaArchiver.Worker;
public class MaskinportenTokenResolver : ITokenResolver
{
    private readonly IOptionsMonitor<MaskinportenTokenResolverOptions> _options;
    private readonly ILogger<MaskinportenTokenResolver> _logger;
    private Dictionary<string, MaskinportenToken?> _maskinportenTokens = new Dictionary<string, MaskinportenToken?>();
    private static readonly SemaphoreSlim _theLock = new SemaphoreSlim(1, 1);

    public MaskinportenTokenResolver(IOptionsMonitor<MaskinportenTokenResolverOptions> options, ILogger<MaskinportenTokenResolver> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(string scope)
    {
        try
        {
            await _theLock.WaitAsync();
            if (_maskinportenTokens.TryGetValue(scope, out MaskinportenToken? maskinportenToken) &&
                maskinportenToken?.IsExpiring() == false) return maskinportenToken.Token;

            var certificate = new X509Certificate2(
                _options.CurrentValue.Certificate.Path,
                _options.CurrentValue.Certificate.Password
            );

            var configuration = new MaskinportenClientConfiguration(
                audience: _options.CurrentValue.Audience,
                tokenEndpoint: _options.CurrentValue.TokenEndpoint,
                issuer: _options.CurrentValue.Issuer,
                numberOfSecondsLeftBeforeExpire: 10,
                certificate: certificate);

            var maskinportenClient = new MaskinportenClient(configuration);

            maskinportenToken = await maskinportenClient.GetAccessToken(scope);
            _logger.LogInformation("Fetched token from Maskinporten");

            if (_maskinportenTokens.ContainsKey(scope)) _maskinportenTokens[scope] = maskinportenToken;
            else _maskinportenTokens.Add(scope, maskinportenToken);

            return maskinportenToken.Token;
        }
        finally
        {
            _theLock.Release();
        }
    }
}
