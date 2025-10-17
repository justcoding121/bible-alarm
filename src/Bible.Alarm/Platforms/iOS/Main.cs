using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.iOS.Services.Platform;
using Bible.Alarm.Services.Infrastructure;
using Foundation;
using MediaManager;
using NLog;
using System;
using System.Threading.Tasks;
using UIKit;

namespace Bible.Alarm.iOS
{
    public class Application
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

        static Application()
        {
            LogSetup.Initialize(VersionFinder.Default, new string[] { }, "iOS");

            AppDomain.CurrentDomain.UnhandledException += unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += unobserverdTaskException;
        }

        private static void unobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.Error(e.Exception, "Unobserved task exception.");
        }

        private static void unhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Error("Unhandled exception.", e.SerializeObject());
        }

        // This is the main entry point of the application.
        static void Main(string[] args)
        {
            try
            {
                // if you want to use a different Application Delegate class from "AppDelegate"
                // you can specify it here.
                UIApplication.Main(args, null, "AppDelegate");
            }
            catch (Exception e)
            {
                logger.Error(e, "Main initialization failed.");
                throw;
            }
        }

        private static bool disposed = false;
        private static void dispose()
        {
            if (disposed)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException -= unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= unobserverdTaskException;

            disposed = true;
        }

        ~Application()
        {
            dispose();
        }
    }
}
