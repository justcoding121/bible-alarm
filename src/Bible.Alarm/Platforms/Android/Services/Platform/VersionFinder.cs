using Android.App;
using Bible.Alarm.Contracts.Platform;
using System;

namespace Bible.Alarm.Droid.Services.Platform
{
    public class VersionFinder : IVersionFinder
    {
        private static readonly Lazy<string> Version = new Lazy<string>(() => GetVersionNameInternal());
        public static VersionFinder Default => new VersionFinder();

        public string GetVersionName()
        {
            return Version.Value;
        }

        private static string GetVersionNameInternal()
        {
            return "Android " + Android.App.Application.Context.ApplicationContext.PackageManager
                  .GetPackageInfo(Android.App.Application.Context.ApplicationContext.PackageName, 0).VersionName;
        }

    }
}