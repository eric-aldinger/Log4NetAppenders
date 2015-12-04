using System.Configuration;
using System.IO;
using System.Threading;
using log4net;
using log4net.Config;
using Topshelf;

namespace RabbitTestHarness
{

    public class RabbitTestService
    {
        public static string logfile = ConfigurationManager.AppSettings["log4net.Config"];
        public static readonly ILog log = LogManager.GetLogger(typeof (RabbitTestService));
        private static string SecurityContext = ConfigurationManager.AppSettings["sw.SecurityContext"];

        public static void Main(string[] args)
        {
            //Enable logging at the earliest part of the entry point
            XmlConfigurator.Configure(new FileInfo(logfile));
            log.Debug("main method entered. Logging enabled");
            ServiceConfiguration();
            log.Debug("end main method entered");
        }

        public static void Pause()
        {
            Consumer.ReceiveMsg();
        }

        public static void ServiceConfiguration()
        {
            Topshelf.Host host = HostFactory.New(svcHost =>
            {
                log.Debug("Enter the host factory");
                svcHost.EnablePauseAndContinue();
                svcHost.EnableShutdown();
                svcHost.Service<Emitter>(svc =>
                {
                    log.Debug("Enter the Windows service constructor");
                    svc.ConstructUsing(() => new Emitter());
                    
                    svc.WhenStarted(tsvc =>
                    {
                        log.Info("Start Rabbit test service");
                        tsvc.Start();
                        tsvc.EmitMsg();
                    });

                    svc.WhenStopped(tsvc =>
                    {
                        log.Debug("Stopping Rabbit test service");
                        tsvc.Stop();
                        log.Info("Stopped Rabbit test service");
                    });

                    svc.WhenPaused(tsvc => tsvc.Pause());

                    svc.WhenContinued(tsvc =>
                    {
                        tsvc.Continue();
                    });

                    svc.WhenShutdown(tsvc =>
                    {
                        Consumer.ReceiveMsg();
                        Thread.Sleep(100);
                        tsvc.Stop();
                    });

                    //svc.StartAutomatically();

                    svcHost.SetDescription("A test harness designed to provide N messages to a queue and consume them.");
                    svcHost.SetDisplayName("Data Services Rabbit Test Harness");
                    svcHost.SetServiceName("DataServicesRabbitTestHarness");
                    svcHost.RunAs(ConfigurationManager.AppSettings["rmq.SvcUser"], ConfigurationManager.AppSettings["rmq.SvcPassword"]);
                });
            });
            host.Run();
        }
    }
}