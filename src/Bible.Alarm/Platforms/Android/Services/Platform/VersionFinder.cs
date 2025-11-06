using AndroidApplication = global::Android.App.Application;
using Bible.Alarm.Common.Interfaces.Platform;

namespace Bible.Alarm.Platforms.Android.Services.Platform;

public class VersionFinder : IVersionFinder
{
    private static readonly Lazy<string> Version = new(() => GetVersionNameInternal());
    public static VersionFinder Default => new();

    public string GetVersionName()
    {
        return Version.Value;
    }

    private static string GetVersionNameInternal()
    {
        return "Android " + AndroidApplication.Context.ApplicationContext.PackageManager
            .GetPackageInfo(AndroidApplication.Context.ApplicationContext.PackageName, 0).VersionName;
    }
}