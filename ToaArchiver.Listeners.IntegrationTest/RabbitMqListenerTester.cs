using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ToaArchiver.Domain.Core.Generic;
using ToaArchiver.Domain.Listeners;

namespace ToaArchiver.Listeners.IntegrationTest
{
    public partial class RabbitMqListenerTester
    {
        private readonly RabbitMqListener _rabbitMqListener;
        private readonly Mock<IInvokeMessageHandler<BasicDeliverEventArgs>> _mockMessageHandlerInvoker = new();
        private readonly Mock<Microsoft.Extensions.Logging.ILogger<RabbitMqListener>> _mockLogger = new();
        private const string _host = "mq-test.dfo.no";
        private const int _port = 5671;
        private const string _queue = "all";

        public RabbitMqListenerTester()
        {
            var connectionFactory = new ConnectionFactory { HostName = _host, Port =  _port, VirtualHost = _virtualHost, UserName = User, Password = Password };
            _rabbitMqListener = new RabbitMqListener(_mockMessageHandlerInvoker.Object, connectionFactory, _mockLogger.Object);
            _rabbitMqListener.Listen(_queue);
        }

        [Fact]
        public void Message_received__Invoke_is_called()
        {
            // Given
            BasicDeliverEventArgs? args = null;
            
            // Assert
            _mockMessageHandlerInvoker.Verify(m => m.Invoke(It.IsAny<BasicDeliverEventArgs>()));
            _mockMessageHandlerInvoker.Setup(m => m.Invoke(It.IsAny<BasicDeliverEventArgs>())).Callback<BasicDeliverEventArgs>(a => args = a);

            Assert.NotNull(args);
        }
    }
}