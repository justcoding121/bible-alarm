using Bible.Alarm.Contracts.Platform;
using System;
using Windows.ApplicationModel;

namespace Bible.Alarm.Uwp.Services.Platform
{
    public class UwpVersionFinder : IVersionFinder
    {
        private readonly static Lazy<string> version = new Lazy<string>(() => getVersionName());
        public static UwpVersionFinder Default => new UwpVersionFinder();

        public string GetVersionName()
        {
            return version.Value;
        }

        private static string getVersionName()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;

            return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        }

    }
}