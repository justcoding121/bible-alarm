using Bible.Alarm.VersionPatcher.Services.Contracts;
using System.IO;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class TestPathService : IPathService
{
    private readonly string _testResourcesPath;

    public TestPathService()
    {
        // TestResources are copied to output directory
        var testOutputDir = Path.GetDirectoryName(typeof(TestPathService).Assembly.Location);
        _testResourcesPath = Path.Combine(testOutputDir ?? "", "TestResources");
    }

    public string GetAndroidManifestPath()
    {
        return Path.Combine(_testResourcesPath, "AndroidManifest.xml");
    }

    public string GetIOSInfoPlistPath()
    {
        return Path.Combine(_testResourcesPath, "Info.plist");
    }

    public string GetWindowsManifestPath()
    {
        return Path.Combine(_testResourcesPath, "Package.appxmanifest");
    }
}
