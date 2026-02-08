#if IOS
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using System.IO;
using System.Text;

namespace Bible.Alarm.Platforms.iOS.Logging;

/// <summary>
/// Custom Serilog sink for iOS that writes logs to a file in the Documents directory
/// (accessible via Finder file sharing) and also maintains a recent log buffer for HTTP access
/// </summary>
public class IOSLogFileSink : Serilog.Core.ILogEventSink, IDisposable
{
    private readonly ITextFormatter formatter;
    private readonly string logFilePath;
    private readonly StreamWriter writer;
    private readonly object lockObject = new();
    private readonly StringBuilder recentLogBuffer = new();
    private const int MaxRecentLogLines = 1000;
    private int currentLineCount = 0;
    private bool disposed;

    public IOSLogFileSink(string logFilePath, ITextFormatter? formatter = null)
    {
        this.logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        this.formatter = formatter ?? new MessageTemplateTextFormatter(
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        // Ensure directory exists
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Open file in append mode with auto-flush
        var fileStream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        writer = new StreamWriter(fileStream, Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    public void Emit(LogEvent logEvent)
    {
        if (disposed || logEvent == null)
        {
            return;
        }

        lock (lockObject)
        {
            try
            {
                using var stringWriter = new StringWriter();
                formatter.Format(logEvent, stringWriter);
                var formattedMessage = stringWriter.ToString();

                // Write to file
                writer.WriteLine(formattedMessage);
                writer.Flush();

                // Maintain recent log buffer (for potential HTTP access)
                recentLogBuffer.AppendLine(formattedMessage);
                currentLineCount++;

                // Keep only recent lines
                if (currentLineCount > MaxRecentLogLines)
                {
                    var lines = recentLogBuffer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > MaxRecentLogLines)
                    {
                        recentLogBuffer.Clear();
                        var recentLines = lines.Skip(lines.Length - MaxRecentLogLines);
                        recentLogBuffer.AppendLine(string.Join("\n", recentLines));
                        currentLineCount = MaxRecentLogLines;
                    }
                }
            }
            catch
            {
                // Silently fail - logging shouldn't crash the app
            }
        }
    }

    /// <summary>
    /// Gets recent log content (last MaxRecentLogLines)
    /// </summary>
    public string GetRecentLogs()
    {
        lock (lockObject)
        {
            return recentLogBuffer.ToString();
        }
    }

    /// <summary>
    /// Gets the full log file path
    /// </summary>
    public string LogFilePath => logFilePath;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        lock (lockObject)
        {
            try
            {
                writer?.Flush();
                writer?.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }

            disposed = true;
        }
    }
}
#endif
