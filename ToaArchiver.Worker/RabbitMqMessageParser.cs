using DfoClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Text;
using ToaArchiver.Domain;
using ToaArchiver.Domain.Core;
using ToaArchiver.Domain.Core.Generic;
using ToaArchiver.Domain.Messages;

namespace ToaArchiver.Worker;

public class RabbitMqMessageParser : IParseMessage<byte[]>
{
    private readonly IArchive _archive;
    private readonly ILogger<RabbitMqMessageParser> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IClient _dfoClient;
    private readonly IServiceProvider _serviceProvider;

    public RabbitMqMessageParser(IClient dfoClient, IArchive archive, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _dfoClient = dfoClient;
        _archive = archive;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<RabbitMqMessageParser>();
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
            messageObj = JToken.Parse(messageString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not parse message {MessageString}", messageString);
            throw;
        }
        string uri = (string)messageObj.uri;

        if (!uri.ToLower().Contains("infokontrakter/filer")) return new DefaultMessageHandler(messageString, _loggerFactory);

        var felternavn = messageObj.feltnavn as JArray;
        if (felternavn?.Any(f => f.ToString() == "status") == false) return new DefaultMessageHandler(messageString, _loggerFactory);

        string id = (string)messageObj.id;
        string validAfter = (string)messageObj.gyldigEtter;

        var contratChangedMessage = new ContractStatusChangedMessage(SequenceNumber: id)
        {
            Uri = uri,
            Id = id,
            ValidAfter = DateTimeOffset.Parse(validAfter),
            RawData = messageString
        };
        var options = _serviceProvider.GetRequiredService<IOptionsMonitor<ToaOptions>>();
        return new ContractStatusChangedHandler(_archive, _dfoClient, contratChangedMessage, options, _loggerFactory);
    }
}
