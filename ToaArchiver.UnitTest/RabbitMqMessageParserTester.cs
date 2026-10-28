using System.Text;
using DfoClient;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ToaArchiver.Domain.Core;
using ToaArchiver.Worker;

namespace ToaArchiver.UnitTest;

public class RabbitMqMessageParserTester
{
    private readonly RabbitMqMessageParser _parser = new(new Mock<IClient>().Object, new Mock<IArchive>().Object, new Mock<IServiceProvider>().Object, NullLoggerFactory.Instance);

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"a string\"")]
    [InlineData("{}")]
    [InlineData("{\"uri\":\"\"}")]
    public async Task Parse_called__With_invalid_message__Returns_handler_that_rejects(string body)
    {
        IHandleMessage handler = _parser.Parse(Encoding.UTF8.GetBytes(body));

        MessageHandlingResult result = await handler.ExecuteAsync();

        Assert.Equal(MessageHandlingOutcome.Reject, result.Outcome);
    }

    [Fact]
    public async Task Parse_called__With_non_contract_uri__Returns_handler_that_ignores()
    {
        IHandleMessage handler = _parser.Parse(Encoding.UTF8.GetBytes("{\"uri\":\"https://api.dfo.no/ansatte/1\",\"id\":\"1\"}"));

        MessageHandlingResult result = await handler.ExecuteAsync();

        Assert.Equal(MessageHandlingOutcome.Ignored, result.Outcome);
    }
}
