#nullable enable

using System.Diagnostics;

namespace Bible.Alarm.UITests.Infrastructure;

/// <summary>
/// adb and simctl calls used by the UI tests. Failures are returned as text so a missing tool does not crash setup.
/// </summary>
public static class DeviceControl
{
    public static void TryClearAndroidPackage(string packageName)
    {
        _ = Run("adb", $"shell pm clear {packageName}", throwOnError: false);
    }

    public static string TryAndroidDiagnostics(string packageName)
    {
        var log = Run("adb", "logcat -d -t 400 -v time", throwOnError: false);
        var sessions = Run("adb", "shell dumpsys media_session", throwOnError: false);
        return $"package={packageName}{Environment.NewLine}{sessions}{Environment.NewLine}{log}";
    }

    public static string TryIosDiagnostics()
    {
        return Run("xcrun", "simctl spawn booted log show --last 5m --style compact", throwOnError: false);
    }

    public static string Adb(string arguments) => Run("adb", arguments, throwOnError: true);

    public static string AdbOrEmpty(string arguments) => Run("adb", arguments, throwOnError: false);

    public static MediaSessionSnapshot AndroidMediaSession(string packageName)
    {
        var dump = Run("adb", "shell dumpsys media_session", throwOnError: false);
        return MediaSessionDump.Parse(dump, packageName);
    }

    public static void AndroidBroadcast(string action, string? extras = null)
    {
        var packageName = Environment.GetEnvironmentVariable("BIBLE_ALARM_APP_PACKAGE")
            ?? Bible.Alarm.UITests.Fixtures.AndroidAppiumFixture.DefaultPackageName;
        var arguments = $"shell am broadcast -a {action} -p {packageName}";
        if (!string.IsNullOrWhiteSpace(extras))
        {
            arguments += " " + extras;
        }

        _ = Run("adb", arguments, throwOnError: true);
    }

    public static string CarHost(string command, string? argument = null)
    {
        var extras = $"--es cmd {command}";
        if (!string.IsNullOrWhiteSpace(argument))
        {
            extras += $" --es arg {argument}";
        }

        _ = Run("adb", "logcat -c", throwOnError: false);
        _ = Run(
            "adb",
            $"shell am start -n com.google.android.carhostsim/com.google.android.projection.gearhead.HarnessActivity {extras}",
            throwOnError: true);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var log = string.Empty;
        var needle = "\"cmd\":\"" + command + "\"";
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(1000);
            log = Run("adb", "logcat -d -t 80 -s CarHostSim:I", throwOnError: false);
            if (log.Contains(needle, StringComparison.Ordinal)
                && (log.Contains("\"ok\":true", StringComparison.Ordinal) || log.Contains("\"ok\":false", StringComparison.Ordinal)))
            {
                return log;
            }
        }

        return log;
    }

    public static void DarwinNotify(string name)
    {
        _ = Run("xcrun", $"simctl spawn booted notifyutil -p {name}", throwOnError: true);
    }

    public static string Run(string fileName, string arguments, bool throwOnError)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(start);
            if (process == null)
            {
                return string.Empty;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);
            if (throwOnError && process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{fileName} {arguments} exited {process.ExitCode}: {stderr}");
            }

            return stdout + stderr;
        }
        catch (Exception ex) when (!throwOnError)
        {
            return ex.Message;
        }
    }
}
