using ToaArchiver.Worker;
using ToaArchiver.Worker.Extensions;

bool replayParked = args.Contains(ParkedMessageReplayer.Argument);
args = args.Where(a => a != ParkedMessageReplayer.Argument).ToArray();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureToaServices()
    .ConfigureServices(ConfigureHostedService)
#if DEBUG
    .ConfigureAppConfiguration(c => c.AddUserSecrets<Program>())
#endif
    .Build();

if (replayParked)
{
    ParkedMessageReplayer.Replay(host.Services);
    return;
}

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