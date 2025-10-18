using Bible.Alarm.Contracts.Platform;
using Foundation;

namespace Bible.Alarm.iOS.Services.Platform
{
    public class VersionFinder : IVersionFinder
    {
        private static readonly Lazy<string> Version = new Lazy<string>(() => VersionName());
        public static VersionFinder Default => new VersionFinder();

        public string GetVersionName()
        {
            return Version.Value;
        }

        private static string VersionName()
        {
            return "iOS " + (NSString)NSBundle.MainBundle.InfoDictionary["CFBundleShortVersionString"];
        }
    }
}