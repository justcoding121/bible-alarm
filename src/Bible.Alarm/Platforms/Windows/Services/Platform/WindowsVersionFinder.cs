using System.Diagnostics;
using System.Reflection;
using Bible.Alarm.Common.Interfaces.Platform;
using Serilog;
using Windows.ApplicationModel;

namespace Bible.Alarm.Platforms.Windows.Services.Platform;

public sealed class WindowsVersionFinder : IVersionFinder
{
    private static readonly Lazy<string> version = new(VersionName);
    public static WindowsVersionFinder Default => new();

    public string GetVersionName() => version.Value;

    private static string VersionName()
    {
        try
        {
            // For WinUI 3 desktop apps, get version from Package.Current (from Package.appxmanifest)
            // This works correctly for both sideloaded apps and Store-published apps.
            // Package.Current reads from the installed package's manifest, which matches
            // the version specified in Package.appxmanifest and distributed through the Store.
            var package = Package.Current;
            if (package != null)
            {
                var packageVersion = package.Id.Version;
                return $"Windows {packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
            }

            // Fallback to assembly version
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
}
