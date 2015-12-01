using System;
using System.Collections.Generic;
using System.Configuration;
using log4net;
using log4net.Core;
using log4net.Layout;
using RabbitMQ.Client;
using RabbitMQExchangeType = RabbitMQ.Client.ExchangeType;

namespace Log4Net.Appenders
{
    public class RabbitMQAppender : RabbitMQAppenderBase
    {
        protected override string Format(LoggingEvent loggingEvent)
        {
            return this.MessageProperties.ContentType.Format(loggingEvent);
        }

        protected override void Debug(string format, params object[] args)
        {
            log4net.Util.LogLog.Debug(typeof(RabbitMQAppender), String.Format(format, args));
        }
    }

    /// <summary>
    /// Properties for creating and configuring a RabbitMQ Exchange.  These are used
    /// when the exchange is declared (typically near startup).
    /// </summary>
    public class ExchangeProperties
    {
        internal ICollection<ExchangeBinding> Bindings { get; private set; }

        /// <summary>
        /// 	Gets or sets the exchange name.
        /// </summary>
        /// <remarks>
        /// 	Default is set in log4net.config
        /// </remarks>
        public string Name { get; set; }

        /// <summary>
        /// 	Gets or sets the exchange type.
        /// </summary>
        /// <remarks>
        /// 	Default is 'topic'
        /// </remarks>
        public string ExchangeType { get; set; }

        /// <summary>
        /// 	Gets or sets the exchange durability.
        /// </summary>
        /// <remarks>
        /// 	Default is false
        /// </remarks>
        public bool Durable { get; set; }

        /// <summary>
        /// 	Gets or sets the exchange auto-delete feature.
        /// </summary>
        /// <remarks>
        /// 	Default is true
        /// </remarks>
        public bool AutoDelete { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ExchangeProperties()
        {
            this.Name = ConfigurationManager.AppSettings["rmq.UserName"]; 
            this.ExchangeType = RabbitMQExchangeType.Topic;
            this.Durable = false;
            this.AutoDelete = true;
            this.Bindings = new List<ExchangeBinding>();
        }

        /// <summary>
        /// Add an ExchangeBinding.
        /// </summary>
        /// <param name="exchangeBinding">The exchangeBinding to add</param>
        public void AddBinding(ExchangeBinding exchangeBinding)
        {
            this.Bindings.Add(exchangeBinding);
        }
    }

    /// <summary>
    /// Defines a binding between "our" exchange another "destination" exchange.
    /// </summary>
    public class ExchangeBinding
    {
        /// <summary>
        /// The name of the destination exchange.
        /// </summary>
        public string Destination { get; set; }

        /// <summary>
        /// The topic (routing key) than controls which messages get sent to the destination exchange.
        /// </summary>
        public string Topic { get; set; }

        public ExchangeBinding()
        {
            this.Topic = "LogTopic";
        }
    }
    
    /// <summary>
    /// Customizable RabbitMQ message properties.
    /// </summary>
    public class MessageProperties
    {
        /// <summary>
        /// Gets or sets the application id to specify when sending. Defaults to null,
        /// and then IBasicProperties.AppId will be the name of the logger instead.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// Get or set the message topic (routing key).  Default value is "%level". 
        /// </summary>
        public PatternLayout Topic { get; set; }

        /// <summary>
        /// Get or set the message content type.  Default value is "text/plain". 
        /// </summary>
        public PatternLayout ContentType { get; set; }

        /// <summary>
        /// Get or set the message delivery mode.  Default is "not persistent". 
        /// </summary>
        public bool Persistent { get; set; }

        /// <summary>
        /// Get or set the message priority.  
        /// Must resolve to a string in the range is "0" - "9".
        /// </summary>
        public PatternLayout Priority { get; set; }

        /// <summary>
        /// Gets or sets whether the logger should log extended data.
        /// Defaults to false.
        /// </summary>
        public bool ExtendedData { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public MessageProperties()
        {
            this.Topic = new PatternLayout(String.Empty);   // when empty the original "Topic" will be used.
            this.ContentType = new PatternLayout("text/plain");
            this.Persistent = false;
            this.Priority = new PatternLayout(String.Empty);  // use default
        }
    }
}