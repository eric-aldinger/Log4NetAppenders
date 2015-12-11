using System;
using System.Configuration;
using System.Net;
using System.Text;
using log4net;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitTestHarness
{
    public class GuestEmitter
    {

        public static readonly ILog log = log4net.LogManager.GetLogger(typeof(GuestEmitter));

        public static string UserName = (ConfigurationManager.AppSettings["rmq.GuestUserName"]);
        public static string Password = (ConfigurationManager.AppSettings["rmq.GuestPassword"]);
        public static int MsgCnt = Int32.Parse(ConfigurationManager.AppSettings["sw.MessageCount"]);
        public static int Port = Int32.Parse(ConfigurationManager.AppSettings["rmq.Port"]);
        public static ushort Heartbeat = ushort.Parse(ConfigurationManager.AppSettings["rmq.Heartbeat"]);
        public static string VHost = ConfigurationManager.AppSettings["rmq.VmHost"];
        public static string HostName = ConfigurationManager.AppSettings["rmq.HostName"];
        public static bool Durability = false;
        public static string RtKey = "Guest_Test_RoutingKey";
        public static string EndpointQueue = ConfigurationManager.AppSettings["rmq.GuestEndpointQueue"];
        public static string Exchange = ConfigurationManager.AppSettings["rmq.GuestEndpointQueue"];
        public static string ExType = "fanout";
        public static bool AutoDelete = true;
        public static IConnection Connection;
        public static IModel Channel;

        public static string Msg = "Hey Chaim, I am the guest/guest default message. If you are seeing me security is not configured as we wanted.";

        public static void Connect()
        {
            log.Debug("Default emitter started: {}");

            var factory = new ConnectionFactory()
            {
                Password = Password,
                UserName = UserName,
                HostName = HostName,
                RequestedHeartbeat = Heartbeat,
                VirtualHost = VHost,
                Port = Port
            };

            factory.AuthMechanisms = new AuthMechanismFactory[] { new PlainMechanismFactory()};
            log.Debug("Default emitter connection factory created as UserName: " + factory.UserName + " HostName: " + factory.HostName + " VirtualHost: " + factory.VirtualHost + " Port:" + factory.Port + " Protocol:" + factory.Protocol);
            
            try
            {
                Connection = factory.CreateConnection();
                Channel = Connection.CreateModel();
                log.Debug("Model created");
            }
                        catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException _unreachable)
            {
                log.Error("Connection failed with {0}", _unreachable);

                if (Connection != null)
                {
                    Connection.Close();
                }
            }
            catch (Exception e)
            {
                log.Error("{0} exception caught during connection.", e);
                if (Connection != null)
                {
                    Connection.Close();
                }
            }



            Channel.ExchangeDeclare(
                exchange: Exchange, 
                type: ExType,
                durable: Durability,
                autoDelete: AutoDelete,
                arguments:null
                );

            Channel.QueueDeclare(
                queue: EndpointQueue,
                durable: Durability,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            Channel.QueueBind(
                queue: EndpointQueue,
                exchange: Exchange,
                routingKey: RtKey);

            log.Debug("queue " + EndpointQueue + " bound to exchange " + Exchange);
        }

        public static void EmitGuestMsg()
        {
            log.Debug("Emit some messages");
            var msBody = Encoding.UTF8.GetBytes(Msg);

            int i = 0;
            while (i++ < MsgCnt)
            {
                Channel.BasicPublish(
                exchange: Exchange,
                routingKey: RtKey,
                basicProperties: null,
                body: msBody);

                log.Debug(" _-_- Enqueued " + Environment.NewLine + Msg);
            }
            log.Debug("Emitter done emitting the emitted messages");
        }

        public static void EatMessages()
        {
            var consumer = new EventingBasicConsumer(Channel);

            consumer.Received += (model, ea) =>
            {
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);
                log.Debug("Received guest message : " + Environment.NewLine + message);
            };

            Channel.BasicConsume(
                queue: EndpointQueue,
                noAck: true,
                consumer: consumer);
        }

        public static void EatQueues()
        {
            log.Debug("removing guest queues and exchanges");
            Channel.QueueDelete(EndpointQueue);
            Channel.ExchangeDelete(Exchange);
            Channel.Close();
            if (Channel.IsClosed == true)
            {
                log.Debug("removed guest queues and exchanges. channel closed");
            }
            else
            {
                log.Error("removing guest queues and exchanges, but channel not closed");
            }
        }
    }
}
