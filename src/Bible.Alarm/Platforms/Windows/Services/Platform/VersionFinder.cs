using System.Diagnostics;
using System.Reflection;
using Bible.Alarm.Common.Interfaces.Platform;

namespace Bible.Alarm.Platforms.Windows.Services.Platform
{
    public class WindowsVersionFinder : IVersionFinder
    {
        private static readonly Lazy<string> Version = new Lazy<string>(() => VersionName());
        public static WindowsVersionFinder Default => new WindowsVersionFinder();

        public string GetVersionName()
        {
            return Version.Value;
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
            catch
            {
                // If all else fails, return a default version
                return "Windows 1.0.0.0";
            }
        }
    }
}