using Bible.Alarm.Common.Interfaces.Platform;
using Foundation;

namespace Bible.Alarm.Platforms.iOS.Services.Platform;

public class iOSVersionFinder : IVersionFinder, IDisposable
{
    private bool _isDisposed;
    private static readonly Lazy<string> Version = new(() => VersionName());
    public static iOSVersionFinder Default => new();

    public string GetVersionName()
    {
        return Version.Value;
    }

    private static string VersionName()
    {
        return "iOS " + (NSString)NSBundle.MainBundle.InfoDictionary["CFBundleShortVersionString"];
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // No resources to dispose
    }
}
