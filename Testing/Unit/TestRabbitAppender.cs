using System;
using log4net.Appender;
using NUnit.Framework;
using Moq;
using log4net.Core;
using Log4Net.Appenders;

namespace Testing
{
    [TestFixture]
    public class TestRabbitAppender:RabbitMQAppender
    {

        [TestFixtureSetUp]
        public void Before()
        {
        }

        [TestFixtureTearDown]
        public void Finally()
        {
        }

        [Test][Ignore("unimplemented")]
        public void Test_ImplementsAppenderSkeleton()
        {
            
            var mockery = new Mock<AppenderSkeleton>();
            var expected = mockery.Object.ErrorHandler;
            var actual = new Mock<RabbitMQAppenderBase>();
            Assert.AreSame(actual.Object.ErrorHandler,expected);
        }


    }
}
