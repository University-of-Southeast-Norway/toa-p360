using DfoClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Text;
using ToaArchiver.Domain;
using ToaArchiver.Domain.Core;
using ToaArchiver.Domain.Core.Generic;

namespace ToaArchiver.Worker;

public class CleanMessagesParser : IParseMessage<byte[]>
{
    private readonly IArchive _archive;
    private readonly ILogger<CleanMessagesParser> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IClient _dfoClient;
    private readonly IServiceProvider _serviceProvider;

    public CleanMessagesParser(IClient dfoClient, IArchive archive, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _dfoClient = dfoClient;
        _archive = archive;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<CleanMessagesParser>();
        _loggerFactory = loggerFactory;
    }

    public IHandleMessage Parse(byte[] message)
    {
        var body = message;
        var messageString = Encoding.UTF8.GetString(body);
        _logger.LogDebug("Raw message body {MessageString}", messageString);
        dynamic messageObj;
        try
        {
            messageObj = JObject.Parse(messageString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not parse message {MessageString}", messageString);
            return new RejectedMessageHandler(messageString, "Message is not a valid JSON object", _loggerFactory);
        }
        string? uri = (string?)messageObj.uri;
        if (string.IsNullOrEmpty(uri)) return new RejectedMessageHandler(messageString, "Message has no uri", _loggerFactory);

        if (!uri.Contains("infokontrakter/filer", StringComparison.CurrentCultureIgnoreCase)) return new AckMessageHandler(messageString, _loggerFactory);
        return new DefaultMessageHandler(messageString, _loggerFactory);
    }
}
