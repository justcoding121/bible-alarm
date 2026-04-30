using System.IO;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class PathService : IPathService
{
    private const string MainAppProjectFolderSegment = "Bible.Alarm";

    private static string GetRepositoryRoot()
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        
        // Find the bible-alarm root directory
        while (currentDir != null && currentDir.Name != "bible-alarm")
        {
            currentDir = currentDir.Parent;
        }
        
        if (currentDir == null)
        {
            throw new DirectoryNotFoundException("Could not find bible-alarm repository root directory");
        }
        
        return currentDir.FullName;
    }
    
    private static readonly string RepositoryRoot = GetRepositoryRoot();

    public string GetAndroidManifestPath() => Path.Combine(RepositoryRoot, "src", MainAppProjectFolderSegment, "Platforms", "Android", "AndroidManifest.xml");

    public string GetIosInfoPlistPath() => Path.Combine(RepositoryRoot, "src", MainAppProjectFolderSegment, "Platforms", "iOS", "Info.plist");

    public string GetWindowsManifestPath() => Path.Combine(RepositoryRoot, "src", MainAppProjectFolderSegment, "Platforms", "Windows", "Package.appxmanifest");

    public string GetCsprojPath() => Path.Combine(RepositoryRoot, "src", MainAppProjectFolderSegment, "Bible.Alarm.csproj");
}
