using Bible.Alarm.Common.Interfaces.Platform;
using AndroidApplication = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Platform;

public class AndroidVersionFinder : IVersionFinder
{
    private static readonly Lazy<string> Version = new(() => GetVersionNameInternal());
    public static AndroidVersionFinder Default => new();

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