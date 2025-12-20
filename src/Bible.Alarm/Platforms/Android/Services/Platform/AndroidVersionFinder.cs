using Bible.Alarm.Common.Interfaces.Platform;
using AndroidApplication = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Platform;

public class AndroidVersionFinder : IVersionFinder, IDisposable
{
    private bool isDisposed;
    private static readonly Lazy<string> version = new(() => GetVersionNameInternal());
    public static AndroidVersionFinder Default => new();

    public string GetVersionName() => version.Value;

    private static string GetVersionNameInternal()
    {
        return "Android " + AndroidApplication.Context.ApplicationContext.PackageManager
            .GetPackageInfo(AndroidApplication.Context.ApplicationContext.PackageName, 0).VersionName;
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
