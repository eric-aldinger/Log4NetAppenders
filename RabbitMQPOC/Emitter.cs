using System;
using System.Configuration;
using System.Net;
using System.Text;
using log4net;
using RabbitMQ.Client;

namespace RabbitTestHarness
{
    public class Emitter
    {

        public static readonly ILog log = LogManager.GetLogger(typeof(Emitter));

        private static string UserName = (ConfigurationManager.AppSettings["rmq.UserName"]);
        private static string Password = (ConfigurationManager.AppSettings["rmq.Password"]);
        private static int MsgCnt = Int32.Parse(ConfigurationManager.AppSettings["sw.MessageCount"]);
        private static int Port = Int32.Parse(ConfigurationManager.AppSettings["rmq.Port"]);
        private static ushort Heartbeat = ushort.Parse(ConfigurationManager.AppSettings["rmq.Heartbeat"]);
        private static string VHost = ConfigurationManager.AppSettings["rmq.VmHost"];
        private static string HostName = ConfigurationManager.AppSettings["rmq.HostName"];
        private static bool Durability = false;
        private static string RtKey = "LDAP_Test_RoutingKey";
        private static string EndpointQueue = ConfigurationManager.AppSettings["rmq.LDAPEndpointQueue"];
        private static string Exchange = ConfigurationManager.AppSettings["rmq.LDAPEndpointQueue"];
        private static string ExType = "fanout";
        private static bool AutoDelete = true;
        private static IConnection Connection;
        private static IModel Model;

        public static string Msg = "_+_+_+_+_This is a message using a LDAP authenticated user._+__+_+_";

        public void Start()
        {
            try
            {
                Emitter.Connect();
            }
            catch
            {
                log.Error("Could not log in as " + UserName + "/" + Password);
            }

            try
            {
                if (Emitter.Connection.IsOpen)
                {
                    EmitMsg();
                }
            }
            catch
            {
                log.Error("Could not queue messages as " + Emitter.UserName + "/" + Emitter.Password);
            }

            try
            {
                GuestEmitter.Connect();
            }
            catch
            {
                log.Error("Could not log in as " + GuestEmitter.UserName + "/" + GuestEmitter.Password);
            }

            try
            {
                if (GuestEmitter.Connection.IsOpen)
                {
                    GuestEmitter.EmitGuestMsg();
                }
            }
            catch
            {
                log.Error("Could not queue messages as " + GuestEmitter.UserName + "/" + GuestEmitter.Password);
            }
        }

        public static void Connect()
        {
            log.Debug("LDAP Emitter starting");

            try
            {
                var factory = new ConnectionFactory()
                {
                    Password = Password,
                    UserName = UserName,
                    HostName = HostName,
                    RequestedHeartbeat = Heartbeat,
                    VirtualHost = VHost,
                    Port = Port
                };

                factory.AuthMechanisms = new AuthMechanismFactory[] { new PlainMechanismFactory() };
                log.Debug("LDAP Emitter connection factory created as UserName: " + factory.UserName + " HostName: " +
                          factory.HostName + " VirtualHost: " + factory.VirtualHost + " Port:" + factory.Port +
                          " Protocol:" + factory.Protocol);

                try
                {
                    Connection = factory.CreateConnection();
                    Model = Connection.CreateModel();
                    log.Debug("LDAP Auth model created");
                }
                catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException _unreachable)
                {
                    log.Error("LDAP connection failed with {0}", _unreachable);

                    if (Connection != null)
                    {
                        Connection.Close();
                    }
                }
                catch (Exception e)
                {
                    log.Error("{0} exception caught during LDAP connection.", e);
                    if (Connection != null)
                    {
                        Connection.Close();
                    }
                }

                Model.ExchangeDeclare(
                    exchange: Exchange,
                    type: ExType,
                    durable: Durability,
                    autoDelete: AutoDelete,
                    arguments: null
                    );

                Model.QueueDeclare(
                    queue: EndpointQueue,
                    durable: Durability,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                Model.QueueBind(
                    queue: EndpointQueue,
                    exchange: Exchange,
                    routingKey: RtKey);

                log.Debug("queue " + Emitter.EndpointQueue + " bound to LDAP  exchange " + Emitter.Exchange);
            }
            catch
            {
                log.Error("Could not log in as " + Emitter.UserName + "/" + Emitter.Password);
            }
        }

        public void EmitMsg()
        {
            try
            {
                var msBody = Encoding.UTF8.GetBytes(Msg);

                int i = 0;
                while (i++ < MsgCnt)
                {
                    Model.BasicPublish(
                        exchange: Exchange,
                        routingKey: RtKey,
                        basicProperties: null,
                        body: msBody);

                    log.Debug(" _-_- Sent " + Msg + " LDAP messages");


                }
                log.Debug("LDAP Emitter done: ");
            }
            catch
            { 
                log.Error("Could not emit LDAP messages");
            }
        }

        public void Pause()
        {
            log.Debug("Pausing");
            Consumer.ReceiveMsg();
            GuestEmitter.EatMessages();
        }

        public void Continue()
        {
            log.Debug("Continue with more msgs");
            EmitMsg();
            GuestEmitter.EmitGuestMsg();
        }

        public void Stop()
        {
            log.Debug("Stopping service");
            try
            {
                Model.QueueDelete(EndpointQueue);
                Model.ExchangeDelete(Exchange);
                Model.Close();
            }
            catch
            {
                log.Info("Could not delete the AD queues");
            }
            try
            {
                GuestEmitter.EatQueues();
            }
            catch
            {
                log.Info("Could not delete the guest queues");
            }

            log.Debug("Service stopped");
        }
    }
}
