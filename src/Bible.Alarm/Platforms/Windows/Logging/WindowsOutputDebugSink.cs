#if WINDOWS

using System.Runtime.InteropServices;
using Serilog;
using Serilog.Events;

namespace Bible.Alarm.Platforms.Windows.Logging;

/// <summary>
/// Serilog sink that writes to Windows OutputDebugString so logs are visible via
/// DebugView (Sysinternals). Avoids file I/O that could block crash reporting.
/// </summary>
public sealed class WindowsOutputDebugSink : Serilog.Core.ILogEventSink
{
    private const string Tag = "BibleAlarm";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern void OutputDebugString(string lpOutputString);

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        if (logEvent.Exception != null)
        {
            message += " " + logEvent.Exception;
        }

        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose or LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error or LogEventLevel.Fatal => "ERR",
            _ => "INF"
        };

        var line = $"[{Tag}] [{level}] {message}";
        OutputDebugString(line);
    }
}

#endif
