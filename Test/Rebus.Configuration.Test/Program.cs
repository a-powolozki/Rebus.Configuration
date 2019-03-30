using System;
using Microsoft.Extensions.Configuration;
using Rebus.Activation;
using Rebus.Config;

namespace Rebus.Configuration.Test
{
    class Program
    {
        static void Main()
        {
            var configuration = new ConfigurationBuilder().AddJsonFile(@"appsettings.json").Build();
            using (var activator = new BuiltinHandlerActivator())
            {

                var unused = Configure
                    .With(activator)
                    //.Transport(t => t.UseRabbitMq("MyConnectionString", "MyQueue").ExchangeNames("DirectExchangeName", "TopicExchangeName"))
                    .ConfigureFrom(configuration);
                //.Logging(c => c.Serilog());
                Console.ReadLine();
            }

            Console.WriteLine("Hello World!");
        }
    }
}
