using System;
using System.Configuration;
using System.Text;
using log4net;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitTestHarness
{
    public class Consumer : Emitter
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Consumer));
        private static string EndpointQueue = ConfigurationManager.AppSettings["rmq.LDAPEndpointQueue"];

        public static string _message;
        public static void ReceiveMsg()
        {

            log.Debug("Consumer started");
            
            ConnectionHelper.Connector();
            var consumer = new EventingBasicConsumer(ConnectionHelper.Model);

                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body;
                    _message = Encoding.UTF8.GetString(body);
                   };

                ConnectionHelper.Model.BasicConsume(
                    queue: EndpointQueue,
                    noAck: true,
                    consumer: consumer);

                log.Debug("Received AD message : " + Environment.NewLine + _message);            
                log.Debug("Consumer complete");
        }
    }
}
