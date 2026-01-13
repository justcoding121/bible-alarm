using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class TestPathService : IPathService
{
    private readonly string testResourcesPath;

    public TestPathService()
    {
        // TestResources are copied to output directory
        var testOutputDir = Path.GetDirectoryName(typeof(TestPathService).Assembly.Location);
        testResourcesPath = Path.Combine(testOutputDir ?? "", "TestResources");
    }

    public string GetAndroidManifestPath() => Path.Combine(testResourcesPath, "AndroidManifest.xml");

    public string GetIosInfoPlistPath() => Path.Combine(testResourcesPath, "Info.plist");

    public string GetWindowsManifestPath() => Path.Combine(testResourcesPath, "Package.appxmanifest");

    public string GetCsprojPath() => Path.Combine(testResourcesPath, "Bible.Alarm.csproj");
}
