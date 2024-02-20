using ToaArchiver.Worker;
using ToaArchiver.Worker.Extensions;

#if !DEBUG
Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
#endif

using IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "ToA Archiver";
    })
    .ConfigureToaServices()
    .ConfigureServices(ConfigureHostedService)
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
try
{
    logger.LogInformation("Service starting.");
    await host.RunAsync();
    logger.LogInformation("Service shutting down.");
}
catch(Exception ex)
{
    logger.LogCritical(ex, "Fatal error occured. Shutting down service.");
    Environment.Exit(1);
}

static void ConfigureHostedService(IServiceCollection services) => services
        .AddHostedService<RabbitMqListenerService>().AddApplicationInsightsTelemetryWorkerService()
        ;