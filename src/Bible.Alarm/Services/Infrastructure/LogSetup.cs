using Bible.Alarm.Contracts.Platform;
using Loggly;
using Loggly.Config;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Services.Infrastructure
{
    public class LogSetup
    {
        private static bool initialized = false;
        private static object @lock = new object();
        public static void Initialize(IVersionFinder versionFinder,
            string[] tags, string device, bool isLoggingEnabled = true)
        {
            CurrentDevice.RuntimePlatform = device;

            if (isLoggingEnabled)
            {
                lock (@lock)
                {
                    if (!initialized)
                    {
                        SetupLoggly();

                        var config = new LoggingConfiguration();
                        var logglyTarget = new NLog.Targets.LogglyTarget();
                        logglyTarget.Tags.Add(new NLog.Targets.LogglyTagProperty()
                        {
                            Name = GetVersionName(versionFinder)
                        });

#if DEBUG
                        logglyTarget.Tags.Add(new NLog.Targets.LogglyTagProperty()
                        {
                            Name = "DEBUG"
                        });
#endif

                        if (tags != null)
                        {
                            foreach (var tag in tags)
                            {
                                logglyTarget.Tags.Add(new NLog.Targets.LogglyTagProperty()
                                {
                                    Name = tag
                                });
                            }

                        }

                        config.AddTarget("loggly", logglyTarget);
                        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, logglyTarget));

                        LogManager.Configuration = config;

                        initialized = true;
                    }
                }
            }
        }
        private static void SetupLoggly()
        {

            var config = LogglyConfig.Instance;
            config.CustomerToken = "ca6fc8af-4beb-4548-8b6c-c5955a288cc6";
            config.ApplicationName = $"Bible-Alarm";

            config.Transport.EndpointHostname = "logs-01.loggly.com";
            config.Transport.EndpointPort = 514;
            config.Transport.LogTransport = LogTransport.SyslogUdp;
            config.ThrowExceptions = false;

            var ct = new ApplicationNameTag();
            ct.Formatter = "application-{0}";
            config.TagConfig.Tags.Add(ct);
        }

        private static string GetVersionName(IVersionFinder versionFinder)
        {
            try
            {
                return versionFinder.GetVersionName();
            }
            catch
            {
                return "AssemblyVersionNotFound";
            }
        }

    }

    public static class JsonConvertExtension
    {
        public static string SerializeObject(this object @object)
        {
            try
            {
                return JsonConvert.SerializeObject(@object);
            }
            catch
            {
                return null;
            }
        }
    }
}