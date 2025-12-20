using System.IO;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class PathService : IPathService
{
    public string GetAndroidManifestPath() => Path.Combine(DirectoryHelper.IndexDirectory, "src", "Bible.Alarm", "Bible.Alarm.Droid", "Properties", "AndroidManifest.xml");

    public string GetIosInfoPlistPath() => Path.Combine(DirectoryHelper.IndexDirectory, "src", "Bible.Alarm", "Bible.Alarm.iOS", "Info.plist");

    public string GetWindowsManifestPath() => Path.Combine(DirectoryHelper.IndexDirectory, "src", "Bible.Alarm", "Platforms", "Windows", "Package.appxmanifest");
}
