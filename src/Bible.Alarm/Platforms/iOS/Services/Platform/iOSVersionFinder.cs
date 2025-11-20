using Bible.Alarm.Common.Interfaces.Platform;
using Foundation;

namespace Bible.Alarm.Platforms.iOS.Services.Platform
{
    public class iOSVersionFinder : IVersionFinder
    {
        private static readonly Lazy<string> Version = new Lazy<string>(() => VersionName());
        public static iOSVersionFinder Default => new iOSVersionFinder();

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