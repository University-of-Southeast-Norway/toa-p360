using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ToaArchiver.Worker.Configurations;

namespace ToaArchiver.Worker;

public static class ParkedMessageReplayer
{
    public const string Argument = "--replay-parked";

    public static int Replay(IServiceProvider services)
    {
        RabbitMqListenerOptions options = services.GetRequiredService<IOptions<RabbitMqListenerOptions>>().Value;
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ParkedMessageReplayer));

        using IConnection connection = services.GetRequiredService<ConnectionFactory>().CreateConnection();
        using IModel model = connection.CreateModel();
        model.ConfirmSelect();
        BrokerRetryScheduler scheduler = new(model, options.Queue, options.Retry);
        int replayed = scheduler.ReplayParked();
        logger.LogInformation("Moved {Replayed} message(s) from {ParkingQueue} to {Queue}", replayed, scheduler.ParkingQueue, options.Queue);
        return replayed;
    }
}
