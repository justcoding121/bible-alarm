#if IOS

using CoreFoundation;
using Serilog;
using Serilog.Events;

namespace Bible.Alarm.Platforms.iOS.Logging;

/// <summary>
/// Serilog sink that writes to Apple Unified Logging (os_log) so logs are visible via
/// Console.app or: log stream --device --predicate 'subsystem == "com.jthomas.info.Bible.Alarm"'
/// </summary>
public sealed class IosSystemLogSink : Serilog.Core.ILogEventSink
{
    private const string Prefix = "BibleAlarm";

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        if (logEvent.Exception != null)
        {
            message += " " + logEvent.Exception;
        }

        var line = $"{Prefix}: {message}";
        switch (logEvent.Level)
        {
            case LogEventLevel.Verbose:
            case LogEventLevel.Debug:
                CoreFoundation.OSLog.Default.Log(OSLogLevel.Debug, line);
                break;
            case LogEventLevel.Information:
                CoreFoundation.OSLog.Default.Log(OSLogLevel.Info, line);
                break;
            case LogEventLevel.Warning:
                CoreFoundation.OSLog.Default.Log(OSLogLevel.Default, line);
                break;
            case LogEventLevel.Error:
                CoreFoundation.OSLog.Default.Log(OSLogLevel.Error, line);
                break;
            case LogEventLevel.Fatal:
                CoreFoundation.OSLog.Default.Log(OSLogLevel.Fault, line);
                break;
            default:
                CoreFoundation.OSLog.Default.Log(OSLogLevel.Info, line);
                break;
        }
    }
}

#endif
