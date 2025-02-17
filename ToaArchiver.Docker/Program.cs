using ToaArchiver.Worker;
using ToaArchiver.Worker.Extensions;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureToaServices()
    .ConfigureServices(ConfigureHostedService)
#if DEBUG
    .ConfigureAppConfiguration(c => c.AddUserSecrets<Program>())
#endif
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
try
{
    logger.LogInformation("Service starting.");
    await host.RunAsync();
    logger.LogInformation("Service shutting down.");
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Fatal error occured. Shutting down service.");
    Environment.Exit(1);
}

static void ConfigureHostedService(IServiceCollection services) => services
        .AddHostedService<RabbitMqListenerService>().AddApplicationInsightsTelemetryWorkerService()
        ;