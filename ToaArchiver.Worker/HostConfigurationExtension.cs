using DfoClient;
using ToaArchiver.Domain.Core.Generic;
using ToaArchiver.Domain.Core;
using ToaArchiver.Archives.P360;
using RabbitMQ.Client;
using P360Client.Domain.Extensions;
using System.Web;
using P360Client.Domain.Configurations;
using ToaArchiver.Worker.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ToaArchiver.Domain;
using P360Client.Extensions;
using Microsoft.Extensions.Configuration;

namespace ToaArchiver.Worker.Extensions;

public static class HostConfigurationExtension
{
    public static IHostBuilder ConfigureToaServices(this IHostBuilder source)
    {
        return source
            .ConfigureServices(ConfigureServices)
            .ConfigureServices(ConfigureP360)
            .ConfigureServices(ConfigureOptions)
            ;
    }

    private static void ConfigureP360(HostBuilderContext context, IServiceCollection collection)
    {
        IP360ClientBuilder builder = collection.AddP360Client();
        const string p360SectionName = "P360";
        var p360Section = context.Configuration.GetSection(p360SectionName);
        IConfigurationSection intarkSection = p360Section.GetSection("Intark");
        if (intarkSection.Exists())
        {
            builder.UseP360ApiWithIntArk($"{p360SectionName}:Intark");
        }
        else
        {
            builder.UseDefaultClientResources($"{p360SectionName}:Sif");
        }
        collection.AddP360Factory()
            .UseJsonTemplateRepository();
    }

    static void ConfigureOptions(HostBuilderContext hostingContext, IServiceCollection services)
    {
        var configurationRoot = hostingContext.Configuration;
        services.Configure<P360ArchiveOptions>(configurationRoot.GetSection(key: "P360:Archive"));
        services.Configure<DfoClientOptions>(configurationRoot.GetSection(key: "Dfo:Api"));
        services.Configure<MaskinportenTokenResolverOptions>(configurationRoot.GetSection(key: "Dfo:Maskinporten"));
        services.Configure<RabbitMqListenerOptions>(configurationRoot.GetSection(key: "Dfo:Queue"));
        services.Configure<ToaOptions>(configurationRoot.GetSection(key: "Toa"));
        services.Configure<List<ApiKeyOptions>>(configurationRoot.GetSection(key: "Dfo:ApiKeys"));
    }

    static void ConfigureServices(IServiceCollection services) => services
            .AddSingleton<ITokenResolver, MaskinportenTokenResolver>()
            .AddSingleton(SetUpConnectionFactory)
            .AddScoped(CreateDfoClient)
            .AddScoped<IArchive, P360Archive>()
            .AddScoped<RabbitMqMessageParser>()
            .AddScoped<CleanMessagesParser>()
            .AddScoped(ConfigureParser)
            .AddScoped<IInvokeMessageHandler<byte[]>, MessageHandlerInvoker<byte[]>>()
            ;

    private static IParseMessage<byte[]> ConfigureParser(IServiceProvider provider)
    {
        IOptions<ToaOptions> options = provider.GetRequiredService<IOptions<ToaOptions>>();
        if (nameof(CleanMessagesParser).Contains(options.Value.MessageParser, StringComparison.InvariantCultureIgnoreCase))
            return provider.GetRequiredService<CleanMessagesParser>();

        return provider.GetRequiredService<RabbitMqMessageParser>();
    }

    private static IClient CreateDfoClient(IServiceProvider serviceProvider)
    {
        var dfoOptions = serviceProvider.GetRequiredService<IOptionsMonitor<DfoClientOptions>>();
        var apiKeyOptions = serviceProvider.GetService<IOptions<List<ApiKeyOptions>>>();
        if (apiKeyOptions?.Value != null && apiKeyOptions.Value.Any()) return new ApiKeyClient(dfoOptions.CurrentValue.BaseAddress, BuildApiKeyProvider(apiKeyOptions.Value));
        else return new JwtAuthorizationDfoClientWrapper(dfoOptions, serviceProvider.GetRequiredService<ITokenResolver>());
    }

    private static IProvideApiKey BuildApiKeyProvider(List<ApiKeyOptions> apiKeyOptions)
    {
        var apiKeyProvider = new ApiKeyListBuilder();
        foreach (var option in apiKeyOptions) apiKeyProvider.WithScope(option.Scope!, option.Header!, option.Key!);
        return apiKeyProvider;
    }

    static ConnectionFactory SetUpConnectionFactory(IServiceProvider serviceProvider)
    {
        IOptions<RabbitMqListenerOptions> configuration = serviceProvider.GetRequiredService<IOptions<RabbitMqListenerOptions>>();
        var uriBuilder = new UriBuilder
        {
            Scheme = configuration.Value.Scheme,
            Host = configuration.Value.Host,
            Port = configuration.Value.Port,
            UserName = configuration.Value.User,
            Password = HttpUtility.UrlEncode(configuration.Value.Password),
            Path = configuration.Value.VirtualHost
        };
        return new ConnectionFactory
        {
            Uri = uriBuilder.Uri
        };
    }
}