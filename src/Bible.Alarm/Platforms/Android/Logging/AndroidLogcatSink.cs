#if ANDROID

using Android.Util;
using Serilog;
using Serilog.Events;

namespace Bible.Alarm.Platforms.Android.Logging;

/// <summary>
/// Serilog sink that writes to Android logcat so logs are visible via adb logcat.
/// Use tag "BibleAlarm" to filter: adb logcat -s BibleAlarm
/// </summary>
public sealed class AndroidLogcatSink : Serilog.Core.ILogEventSink
{
    private const string Tag = "BibleAlarm";

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        if (logEvent.Exception != null)
        {
            message += " " + logEvent.Exception;
        }

        switch (logEvent.Level)
        {
            case LogEventLevel.Verbose:
            case LogEventLevel.Debug:
                global::Android.Util.Log.Debug(Tag, message);
                break;
            case LogEventLevel.Information:
                global::Android.Util.Log.Info(Tag, message);
                break;
            case LogEventLevel.Warning:
                global::Android.Util.Log.Warn(Tag, message);
                break;
            case LogEventLevel.Error:
            case LogEventLevel.Fatal:
                global::Android.Util.Log.Error(Tag, message);
                break;
            default:
                global::Android.Util.Log.Info(Tag, message);
                break;
        }
    }
}

#endif
