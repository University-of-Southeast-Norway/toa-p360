using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using Serilog;
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
    //.ConfigureServices(services =>
    //{
    //    LoggerProviderOptions.RegisterProviderOptions<
    //        EventLogSettings, EventLogLoggerProvider>(services);
    //})
    //.ConfigureLogging((context, logging) =>
    //{
    //    // See: https://github.com/dotnet/runtime/issues/47303
    //    logging.AddConfiguration(
    //        context.Configuration.GetSection("Logging"));
    //})
    .Build();

HostConfigurationExtension.ConfigureSerilogFromConfiguration(host.Services.GetRequiredService<IConfiguration>());

try
{
    Log.Information("Service starting.");
    await host.RunAsync();
    Log.Information("Service shutting down.");
}
catch(Exception ex)
{
    Log.Fatal(ex, "Fatal error occured. Shutting down service.");
    Environment.Exit(1);
}
finally
{
    Log.CloseAndFlush();
}

static void ConfigureHostedService(IServiceCollection services) => services
        .AddHostedService<RabbitMqListenerService>()
        ;