using Bible.Alarm.Common.Interfaces.Platform;
using Foundation;

namespace Bible.Alarm.Platforms.iOS.Services.Platform;

public sealed class IOsVersionFinder : IVersionFinder
{
    private static readonly Lazy<string> version = new(VersionName);
    public static IOsVersionFinder Default => new();

    public string GetVersionName() => version.Value;

    private static string VersionName() => "iOS " + (NSString)NSBundle.MainBundle.InfoDictionary["CFBundleShortVersionString"];
}
