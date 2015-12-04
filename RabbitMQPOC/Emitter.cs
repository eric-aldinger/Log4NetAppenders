using System;
using System.Configuration;
using System.Text;
using log4net;
using RabbitMQ.Client;

namespace RabbitTestHarness
{
    public class Emitter
    {

        public static readonly ILog log = log4net.LogManager.GetLogger(typeof(Emitter));

        public static string UserName = (ConfigurationManager.AppSettings["rmq.UserName"]);
        public static string Password = (ConfigurationManager.AppSettings["rmq.Password"]);
        public static int MsgCnt = Int32.Parse(ConfigurationManager.AppSettings["sw.MessageCount"]);
        public static int Port = Int32.Parse(ConfigurationManager.AppSettings["rmq.Port"]);
        public static ushort Heartbeat = ushort.Parse(ConfigurationManager.AppSettings["rmq.Heartbeat"]);
        public static string VHost = ConfigurationManager.AppSettings["rmq.VmHost"];
        public static string HostName = ConfigurationManager.AppSettings["rmq.HostName"];
        public static bool Durability = false;
        public static string RtKey = "DS_Test_RoutingKey";
        public static string EndpointQueue = "DS_Test_Delete_Me";
        public static string Exchange = "DS_Test_Delete_Me_Ex";
        public static string ExType = "fanout";
        public static bool AutoDelete = true;
        public static IConnection Connection;
        public static IModel Model;

        public static string Msg = 
          @"{_id:ObjectId('563a7a87e2548b102838aee3'),
            'FileName':'TimeslipSample_2015-11-04T21.37.11.8271792.csv',
            'DateReceived':
            ISODate('2015-11-04T21:37:11.827Z'),
            'RowCount':436,
            'ProcessStatus':'File-level validation complete.'}";

        public void Start()
        {
            log.Info("Emitter started: {}");

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
            log.Info("Emitter connection factory created as UserName: " + factory.UserName + " HostName: " + factory.HostName + " VirtualHost: " + factory.VirtualHost + " Port:" + factory.Port + " Protocol:" + factory.Protocol);
            
            try
            {
                Connection = factory.CreateConnection();
                Model = Connection.CreateModel();
                log.Debug("Model created");
            }
                        catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException _unreachable)
            {
                log.Error("Connection failed with {0}", _unreachable);

                if (Connection != null)
                {
                    Connection.Close();
                }
                Environment.Exit(100);
            }
            catch (Exception e)
            {
                log.Error("{0} exception caught during connection.", e);
                if (Connection != null)
                {
                    Connection.Close();
                }
                Environment.Exit(100);
            }



            Model.ExchangeDeclare(
                exchange: Exchange, 
                type: ExType,
                durable: Durability,
                autoDelete: AutoDelete,
                arguments:null
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

            log.Info("queue " + EndpointQueue + " bound to exhcnage " + Exchange);
        }

        public void EmitMsg()
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

                log.Debug(" _-_- Sent "+Msg+" messages");
            }
            log.Info("Emitter done: ");
        }

        public void Pause()
        {
            log.Debug("Pausing");
            Consumer.ReceiveMsg();
        }

        public void Continue()
        {
            log.Debug("Continue with more msgs");
            EmitMsg();
        }

        public void Stop()
        {
            log.Debug("Stopping service");
            Model.QueueDelete(EndpointQueue);
            Model.ExchangeDelete(Exchange);
            Model.Close();
            log.Debug("Service stopped");
        }
    }
}
