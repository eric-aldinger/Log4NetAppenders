using System;
using System.Text;
using log4net;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitTestHarness
{
    public class Receiver : Emitter
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Receiver));
        public static void ReceiveMsg()
        {
            var hostName = HostName;
            var factory = new ConnectionFactory() {HostName = hostName};

            log.Info("Receiver started");
            
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {

                var consumer = new EventingBasicConsumer(channel);

                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body;
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine(" [x] Received {0}", message);
                   };

                channel.BasicConsume(
                    queue: EndpointQueue,
                    noAck: true,
                    consumer: consumer);
                Console.WriteLine(" Press [enter] to exit.");
                Console.ReadLine();
            
            }

            log.Info("Receiver complete");
        }
    }
}
