using Moq;
using Serilog;
using Serilog.Extensions.Logging;
using Xunit.Abstractions;

namespace ToaArchiver.Domain.UnitTest;

public class DefaultMessageHandlerTester
{
    [Fact]
    public void ExecuteCalled__Logs_message()
    {
        // Given
        var mockTestOutputHelper = MockLogger();
        string message = "{message}";
        Microsoft.Extensions.Logging.ILogger microsoftLogger = new SerilogLoggerFactory(ConfigureLogger(mockTestOutputHelper.Object)).CreateLogger(nameof(DefaultMessageHandlerTester));
        DefaultMessageHandler handler = new(message, microsoftLogger);

        // When
        handler.Execute();

        // Then
        mockTestOutputHelper.Verify(t => t.WriteLine(It.Is<string>(s => s.Contains(" INF] Executing messagehandler ToaArchiver.Domain.DefaultMessageHandler"))), Times.Once);
        mockTestOutputHelper.Verify(t => t.WriteLine(It.Is<string>(s => s.Contains(" DBG] ...on message {message}"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsyncCalled__Logs_message()
    {
        // Given
        var mockTestOutputHelper = MockLogger();
        string message = "{message}";
        Microsoft.Extensions.Logging.ILogger microsoftLogger = new SerilogLoggerFactory(ConfigureLogger(mockTestOutputHelper.Object)).CreateLogger(nameof(DefaultMessageHandlerTester));
        DefaultMessageHandler handler = new(message, microsoftLogger);

        // When
        await handler.ExecuteAsync();

        // Then
        mockTestOutputHelper.Verify(t => t.WriteLine(It.Is<string>( s=> s.Contains(" INF] Executing messagehandler ToaArchiver.Domain.DefaultMessageHandler"))), Times.Once);
        mockTestOutputHelper.Verify(t => t.WriteLine(It.Is<string>( s=> s.Contains(" DBG] ...on message {message}"))), Times.Once);
    }

    private Mock<ITestOutputHelper> MockLogger()
    {
        Mock<ITestOutputHelper> mockTestOutputHelper = new Mock<ITestOutputHelper>();
        return mockTestOutputHelper;
    }

    private static ILogger ConfigureLogger(ITestOutputHelper testOutputHelper)
    {
        return new LoggerConfiguration().MinimumLevel.Debug().WriteTo.TestOutput(testOutputHelper).CreateLogger();
    }
}