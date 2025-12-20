using Bible.Alarm.Common.Interfaces.Platform;
using Foundation;

namespace Bible.Alarm.Platforms.iOS.Services.Platform;

public class IOsVersionFinder : IVersionFinder, IDisposable
{
    private bool isDisposed;
    private static readonly Lazy<string> version = new(() => VersionName());
    public static IOsVersionFinder Default => new();

    public string GetVersionName()
    {
        return version.Value;
    }

    private static string VersionName()
    {
        return "iOS " + (NSString)NSBundle.MainBundle.InfoDictionary["CFBundleShortVersionString"];
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
