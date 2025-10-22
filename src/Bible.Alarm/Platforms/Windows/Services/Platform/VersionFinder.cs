using Bible.Alarm.Contracts.Platform;
using Windows.ApplicationModel;

namespace Bible.Alarm.Platforms.Windows.Services.Platform
{
    public class UwpVersionFinder : IVersionFinder
    {
        private static readonly Lazy<string> Version = new Lazy<string>(() => VersionName());
        public static UwpVersionFinder Default => new UwpVersionFinder();

        public string GetVersionName()
        {
            return Version.Value;
        }

        private static string VersionName()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;

            return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        }
    }
}