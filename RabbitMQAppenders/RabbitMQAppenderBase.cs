using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using log4net.Appender;
using log4net.Core;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;

namespace Log4Net.Appenders
{
    /// <summary>
    /// An appender for log4net that publishes to RabbitMQ.
    /// 
    /// To enable assembly bind failure logging, set the registry value [HKLM\Software\Microsoft\Fusion!EnableLog] (DWORD) to 1.
    /// There is some performance penalty associated with assembly bind failure logging.
    /// To turn this feature off, remove the registry value [HKLM\Software\Microsoft\Fusion!EnableLog].
    /// </summary>
    public abstract class RabbitMQAppenderBase : AppenderSkeleton
    {

        private ExchangeProperties _ExchangeProperties = new ExchangeProperties();
        private MessageProperties _MessageProperties = new MessageProperties();

        public static string UserName = ConfigurationManager.AppSettings["rmq.UserName"];
        public static string Password = ConfigurationManager.AppSettings["rmq.Password"];
        public static int Port = Int32.Parse(ConfigurationManager.AppSettings["rmq.Port"]);
        public static ushort Heartbeat = ushort.Parse(ConfigurationManager.AppSettings["rmq.Heartbeat"]);
        public static string SvcUser = ConfigurationManager.AppSettings["rmq.SvcUser"];
        public static string VHost = ConfigurationManager.AppSettings["rmq.VmHost"];
        public static bool Durability = false;

        private IConnection _Connection;
        private IModel _Model;
        private readonly Encoding _Encoding = Encoding.UTF8;
        private readonly DateTime _Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        #region Properties

        private string _Topic = "Logging.ThisApp.Topic";

        /// <summary>
        /// 	Gets or sets the routing key (aka. topic) with which
        /// 	to send messages. Defaults to {0}, which in the end is 'error' for log.Error("..."), and
        /// 	so on. An example could be setting this property to 'ApplicationType.MyApp.Web.{0}'.
        ///		The default is '{0}'.
        ///		
        ///     Superceeded by ExchangeProperties.Topic which is a bit more flexible.
        /// </summary>
        public string Topic
        {
            get { return _Topic; }
            set { _Topic = value; }
        }

        private IProtocol _Protocol = Protocols.DefaultProtocol;

        /// <summary>
        /// Currently does nothing as there is only one protocol supported 
        /// by Pivotal Rabbit client Protocals class. There are other 
        /// protocols supported by Rabbit, but I think you would use a different client.
        /// </summary>
        public IProtocol Protocol
        {
            get { return _Protocol; }
            set { if (value != null) _Protocol = value; }
        }

        private string _hostName = ConfigurationManager.AppSettings["rmq.HostName"];

        /// <summary>
        /// 	Gets or sets the host name of the broker to log to.
        /// </summary>
        /// <remarks>
        /// 	Default is 'localhost'
        /// </remarks>
        public string HostName
        {
            get { return _hostName; }
            set { if (value != null) _hostName = value; }
        }

        /// <summary>
        /// 	Gets or sets the exchange to bind the logger output to.
        /// </summary>
        /// <remarks>
        /// 	Default is 'app-logging'
        /// </remarks>
        public string Exchange
        {
            get { return this.ExchangeProperties.Name; }
            set { if (value != null) this.ExchangeProperties.Name = value; }
        }

        public string AppId
        {
            get { return this._MessageProperties.AppId; }
            set { this._MessageProperties.AppId = value; }
        }

        /// <summary>
        /// Gets or sets whether the logger should log extended data.
        /// Defaults to false.
        /// </summary>
        public bool ExtendedData
        {
            get
            {
                return this.MessageProperties.ExtendedData;
            }
            set
            {
                this.MessageProperties.ExtendedData = value;
            }
        }

        /// <summary>
        /// 	Gets or sets message properties used when messages are published.
        /// </summary>
        public ExchangeProperties ExchangeProperties
        {
            get
            {
                return this._ExchangeProperties;
            }
            set
            {
                this._ExchangeProperties = value ?? new ExchangeProperties();
            }
        }

        /// <summary>
        /// 	Gets or sets message properties used when messages are published.
        /// </summary>
        public MessageProperties MessageProperties
        {
            get
            {
                return this._MessageProperties;
            }
            set
            {
                this._MessageProperties = value ?? new MessageProperties();
            }
        }

        #endregion

        protected override void Append(LoggingEvent loggingEvent)
        {
            try
            {
                if (_Model == null)
                    StartConnection();
            }
            catch (Exception e)
            {
                ErrorHandler.Error("Could not start connection", e);
            }

            if (_Model == null)
                return;

            IBasicProperties basicProperties = GetBasicProperties(loggingEvent);
            byte[] message = GetMessage(loggingEvent);
            string topic = GetTopic(loggingEvent);

            _Model.BasicPublish(this._ExchangeProperties.Name, topic,
                    true, false, basicProperties,
                    message);

        }

        private IBasicProperties GetBasicProperties(LoggingEvent loggingEvent)
        {
            var basicProperties = _Model.CreateBasicProperties();
            basicProperties.ContentEncoding = _Encoding.EncodingName.ToLower();  // "utf8" because this is our _Encoding type
            basicProperties.ContentType = Format(loggingEvent);
            basicProperties.AppId = MessageProperties.AppId ?? loggingEvent.LoggerName;
            basicProperties.Persistent = _MessageProperties.Persistent;

            this.InitMessagePriority(basicProperties, loggingEvent);

            basicProperties.Timestamp = new AmqpTimestamp(
                Convert.ToInt64((loggingEvent.TimeStamp - _Epoch).TotalSeconds));

            if (this.MessageProperties.ExtendedData)
            {
                basicProperties.Headers = new Dictionary<string, object>();
                basicProperties.Headers["ClassName"] = loggingEvent.LocationInformation.ClassName;
                basicProperties.Headers["FileName"] = loggingEvent.LocationInformation.FileName;
                basicProperties.Headers["MethodName"] = loggingEvent.LocationInformation.MethodName;
                basicProperties.Headers["LineNumber"] = loggingEvent.LocationInformation.LineNumber;
            }

            return basicProperties;
        }
        private void InitMessagePriority(IBasicProperties basicProperties, LoggingEvent loggingEvent)
        {
            // the priority must resolve to 0 - 9. Otherwise stick with the default.
            string sPriority = null;

            if (this.MessageProperties.Priority != null)
            {
                var sb = new StringBuilder();
                using (var sw = new StringWriter(sb))
                {
                    this.MessageProperties.Priority.Format(sw, loggingEvent);
                    sPriority = sw.ToString();
                }
            }

            int priority = -1;
            if (Int32.TryParse(sPriority, out priority))
            {
                if ((priority >= 0) && (priority <= 9))
                {
                    basicProperties.Priority = (byte)priority;
                }
            }
        }

        private byte[] GetMessage(LoggingEvent loggingEvent)
        {
            var sb = new StringBuilder();
            using (var sr = new StringWriter(sb))
            {
                Layout.Format(sr, loggingEvent);

                if (Layout.IgnoresException && loggingEvent.ExceptionObject != null)
                    sr.Write(loggingEvent.GetExceptionString());

                return _Encoding.GetBytes(sr.ToString());
            }
        }

        protected abstract string Format(LoggingEvent loggingEvent);

        private string GetTopic(LoggingEvent loggingEvent)
        {
            // format the topic, use the TopicLayout if it's set....
            string topic = null;
            if (this.MessageProperties.Topic != null)
            {
                var sb = new StringBuilder();
                using (var sw = new StringWriter(sb))
                {
                    this.MessageProperties.Topic.Format(sw, loggingEvent);
                    topic = sw.ToString();
                }
            }
            // ...and default back to the Topic format if TopicLayout is not set.
            if (String.IsNullOrEmpty(topic))
            {
                topic = String.Format(_Topic, loggingEvent.Level.Name);
            }

            return topic;
        }

        #region StartUp and ShutDown

        public override void ActivateOptions()
        {
            base.ActivateOptions();
            StartConnection();
        }

        private void StartConnection()
        {
            try
            {
                RabbitMQ.Client.ConnectionFactory factory = GetConnectionFac();
                factory.UseBackgroundThreadsForIO = true;
                _Connection = factory.CreateConnection();
                _Connection.ConnectionShutdown += ShutdownAmqp;

                try { _Model = _Connection.CreateModel(); }
                catch (Exception e)
                {
                    ErrorHandler.Error("could not create model", e);
                }
            }
            catch (Exception e)
            {
                ErrorHandler.Error("could not connect to Rabbit instance", e);
            }

            if (_Model != null)
            {
                _Model.ExchangeDeclare(
                    this.ExchangeProperties.Name, 
                    this.ExchangeProperties.ExchangeType, 
                    this.ExchangeProperties.Durable, 
                    this.ExchangeProperties.AutoDelete, 
                    null);

                foreach (ExchangeBinding exchangeBinding in this.ExchangeProperties.Bindings)
                {
                    _Model.ExchangeBind(exchangeBinding.Destination, this.ExchangeProperties.Name, exchangeBinding.Topic);
                }

                _Model.ModelShutdown += OnModelShutdown;
                this.Debug("apqp connection opened");
            }
        }


        private ConnectionFactory GetConnectionFac()
        {
            return new ConnectionFactory
            {
                HostName = HostName,
                VirtualHost = VHost,
                UserName = UserName,
                Password = Password,
                Ssl = Ssl,
                RequestedHeartbeat = Heartbeat,
                Port = Port
            };
        }

        private SslOption _Ssl = new SslOption();

        protected SslOption Ssl {
            get { return _Ssl; }
            set { _Ssl = value; }
        }

        protected override void OnClose()
        {
            base.OnClose();

            ShutdownAmqp(_Connection,
                         new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ReplySuccess, "closing appender"));
        }

        private void OnModelShutdown(object sender, ShutdownEventArgs reason)
        {
            var model = (IModel)sender;
            if (Object.ReferenceEquals(this._Model, model))
            {
                this.ShutdownAmqp(this._Connection, reason);
            }
        }

        private void ShutdownAmqp(object sender, ShutdownEventArgs reason)
        {
            var connection = (IConnection)sender;

            if (Object.ReferenceEquals(this._Connection, connection))
            {
                // If this method is called through _Connection.ConnectionShutdown, calling Model.Close() 
                // may deadlock. I believe what is happening is the Close() call is waiting for the 
                // close process to complete, but the close process is in the middle of the shutdown event 
                // and is waiting for this method to return (which is waiting for the close process to
                // complete...) So...
                //
                // Here we are just going to set our references to the model and connection to null,
                // and then clean up the old model and connection asynchronously. That allows this method
                // to return quickly, which lets the close process complete.  

                this.BeginShutdownAmqp(this._Connection, this._Model, reason);

                this._Connection = null;
                this._Model = null;
            }
        }

        private delegate void ShutdownAmqpCallback(IConnection connection, IModel model, ShutdownEventArgs reason);
        private void BeginShutdownAmqp(IConnection connection, IModel model, ShutdownEventArgs reason)
        {
            ShutdownAmqpCallback shutdownCallback = this.ShutdownAmqp;
            shutdownCallback.BeginInvoke(connection, model, reason, null, null);
        }
        private void ShutdownAmqp(IConnection connection, IModel model, ShutdownEventArgs reason)
        {
            try
            {
                if (connection != null)
                {
                    connection.ConnectionShutdown -= ShutdownAmqp;
                    connection.AutoClose = true;
                }

                if (model != null)
                {
                    model.ModelShutdown -= this.OnModelShutdown;
                    this.Debug("closing amqp model. {0}.", reason);
                    model.Close(Constants.ReplySuccess, "closing rabbitmq appender, shutting down logging");
                    model.Dispose();

                    this.Debug("amqp model closed.");
                }
            }
            catch (Exception e)
            {
                ErrorHandler.Error("could not close model", e);
            }
        }

        #endregion

        protected abstract void Debug(string format, params object[] args);
    }
}