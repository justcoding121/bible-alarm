using Bible.Alarm.Contracts.Platform;
using Foundation;
using System;

namespace Bible.Alarm.iOS.Services.Platform
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
            return "iOS " + (NSString)NSBundle.MainBundle.InfoDictionary["CFBundleShortVersionString"];
        }

    }
}