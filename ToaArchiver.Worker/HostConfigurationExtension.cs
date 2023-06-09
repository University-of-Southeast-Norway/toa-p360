using DfoClient;
using RabbitMQ.Client.Events;
using ToaArchiver.Domain.Core.Generic;
using ToaArchiver.Domain.Core;
using ToaArchiver.Listeners.Parsers;
using ToaArchiver.Archives.P360;
using RabbitMQ.Client;
using P360Client.Domain.Extensions;
using System.Web;
using Serilog;
using Serilog.Events;
using P360Client.Domain.Configurations;
using ToaArchiver.Worker.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ToaArchiver.Domain;

namespace ToaArchiver.Worker.Extensions;

public static class HostConfigurationExtension
{
    public static IHostBuilder ConfigureToaServices(this IHostBuilder source)
    {
        return source.ConfigureServices(ConfigureServices)
            .ConfigureServices(ConfigureOptions)
            .UseSerilog();
    }

    static void ConfigureOptions(HostBuilderContext hostingContext, IServiceCollection services)
    {
        var configurationRoot = hostingContext.Configuration;
        services.Configure<P360Client.Configurations.ClientOptions>(configurationRoot.GetSection(key: "P360:ClientOptions"));
        services.Configure<AppendCaseOptions>(configurationRoot.GetSection(key: "P360:AppendCaseOptions"));
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
            .AddP360DomainResourcesScoped()
            .UseDefaultP360ClientResourcesScoped()
            .UseP360DomainJsonTemplateRepositoryScoped()
            .AddScoped<IArchive, P360Archive>()
            .AddScoped<IParseMessage<byte[]>, RabbitMqMessageParser>()
            .AddScoped<IInvokeMessageHandler<byte[]>, MessageHandlerInvoker<byte[]>>()
            ;

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

    public static void ConfigureSerilogFromConfiguration(IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Destructure.ByTransforming<BasicDeliverEventArgs>(
                r => new
                {
                    r.Exchange,
                    r.ConsumerTag,
                    r.DeliveryTag,
                    r.Redelivered,
                    r.RoutingKey,
                    BasicProperties = new
                    {
                        r.BasicProperties?.MessageId,
                        r.BasicProperties?.AppId,
                        r.BasicProperties?.Priority,
                        r.BasicProperties?.Persistent,
                        r.BasicProperties?.ProtocolClassId,
                        r.BasicProperties?.ReplyTo,
                        r.BasicProperties?.Timestamp,
                        r.BasicProperties?.Type,
                        r.BasicProperties?.UserId
                    }
                })
            .CreateLogger();
    }

    public static void ConfigureSerilogConsole()
    {
        Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.FromLogContext()
                    .Destructure.ByTransforming<BasicDeliverEventArgs>(
                        r => new
                        {
                            r.Exchange,
                            r.ConsumerTag,
                            r.DeliveryTag,
                            r.Redelivered,
                            r.RoutingKey,
                            BasicProperties = new
                            {
                                r.BasicProperties?.MessageId,
                                r.BasicProperties?.AppId,
                                r.BasicProperties?.Priority,
                                r.BasicProperties?.Persistent,
                                r.BasicProperties?.ProtocolClassId,
                                r.BasicProperties?.ReplyTo,
                                r.BasicProperties?.Timestamp,
                                r.BasicProperties?.Type,
                                r.BasicProperties?.UserId
                            }
                        })
                    .WriteTo.Console()
                    .CreateLogger();
    }
}