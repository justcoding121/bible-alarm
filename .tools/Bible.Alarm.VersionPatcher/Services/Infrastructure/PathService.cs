using System.IO;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class PathService : IPathService
{
    public string GetAndroidManifestPath()
    {
        return Path.Combine(DirectoryHelper.IndexDirectory, "src", "Bible.Alarm", "Bible.Alarm.Droid", "Properties", "AndroidManifest.xml");
    }

    public string GetIosInfoPlistPath()
    {
        return Path.Combine(DirectoryHelper.IndexDirectory, "src", "Bible.Alarm", "Bible.Alarm.iOS", "Info.plist");
    }

    public string GetWindowsManifestPath()
    {
        return Path.Combine(DirectoryHelper.IndexDirectory, "src", "Bible.Alarm", "Platforms", "Windows", "Package.appxmanifest");
    }
}
