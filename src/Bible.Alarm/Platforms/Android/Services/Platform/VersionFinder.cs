using Android.App;
using Bible.Alarm.Contracts.Platform;
using System;

namespace Bible.Alarm.Droid.Services.Platform
{
    public class VersionFinder : IVersionFinder
    {
        private readonly static Lazy<string> version = new Lazy<string>(() => getVersionName());
        public static VersionFinder Default => new VersionFinder();

        public string GetVersionName()
        {
            return version.Value;
        }

        private static string getVersionName()
        {
            return "Android " + Application.Context.ApplicationContext.PackageManager
                  .GetPackageInfo(Application.Context.ApplicationContext.PackageName, 0).VersionName;
        }

    }
}