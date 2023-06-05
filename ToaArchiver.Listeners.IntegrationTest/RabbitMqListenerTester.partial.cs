using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToaArchiver.Listeners.IntegrationTest
{
    public partial class RabbitMqListenerTester
    {
        private const string _password = "<insert RabbitMQ password>";
        private const string _user = "<insert RabbitMQ user>";
        private const string _virtualHost = "";

        public static string Password => _password == "<insert RabbitMQ password>" ? throw new Exception("Insert RabbitMQ password.") : _password;

        public static string User => _user == "<insert RabbitMQ user>" ? throw new Exception("Insert RabbitMQ user.") : _user;
    }
}
