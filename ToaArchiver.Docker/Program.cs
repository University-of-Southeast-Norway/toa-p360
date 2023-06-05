using ToaArchiver.Worker;
using ToaArchiver.Worker.Extensions;

HostConfigurationExtension.ConfigureSerilogConsole();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureToaServices()
    .ConfigureServices(ConfigureHostedService)
    .Build();

await host.RunAsync();

static void ConfigureHostedService(IServiceCollection services) => services
        .AddHostedService<RabbitMqListenerService>()
        ;