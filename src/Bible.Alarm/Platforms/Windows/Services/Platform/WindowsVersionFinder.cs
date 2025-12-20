using System.Diagnostics;
using System.Reflection;
using Bible.Alarm.Common.Interfaces.Platform;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Services.Platform;

public class WindowsVersionFinder : IVersionFinder, IDisposable
{
    private bool isDisposed;
    private static readonly Lazy<string> version = new(() => VersionName());
    public static WindowsVersionFinder Default => new();

    public string GetVersionName()
    {
        return version.Value;
    }

    private static string VersionName()
    {
        try
        {
            // For WinUI 3 desktop apps, we can get version from the assembly
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;

            if (version != null)
            {
                return $"Windows {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }

            // Fallback to file version if assembly version is not available
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            if (!string.IsNullOrEmpty(fileVersionInfo.FileVersion))
            {
                return $"Windows {fileVersionInfo.FileVersion}";
            }

            return "Windows 1.0.0.0";
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Failed to get Windows version, using default version");
            // If all else fails, return a default version
            return "Windows 1.0.0.0";
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // No resources to dispose
    }
}
