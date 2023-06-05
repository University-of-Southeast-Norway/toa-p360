using System.Text.Json;
using System.Text.Json.Nodes;

namespace ToaArchiver.Domain.Messages;

public abstract record class MessageBase(string Id = default!, string CorrelationId = default!, string Source = default!, string Subject = default!, string Uri = default!, DateTimeOffset ValidAfter = default)
{
    private readonly string _rawData = default!;
    public string RawData {
        get => _rawData;
        init
        {
            _rawData = value;
            if (!string.IsNullOrEmpty(value))JsonData = JsonNode.Parse(value);
        }
    }
    public JsonNode? JsonData { get; private init; }
}
