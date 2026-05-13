#nullable enable

using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;

namespace Bible.Alarm.VersionPatcher.Tests;

public sealed class ReleaseVersionPatcherTests
{
    private sealed class MemoryFileService : IFileService
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string filePath) => Files.ContainsKey(filePath);

        public Task<string> ReadFileAsync(string filePath) =>
            Task.FromResult(Files[filePath]);

        public Task WriteFileAsync(string filePath, string content)
        {
            Files[filePath] = content;
            return Task.CompletedTask;
        }
    }

    private sealed class StubPathService(string csprojPath) : IPathService
    {
        public string GetAndroidManifestPath() => @"X:\z\AndroidManifest.xml";

        public string GetIosInfoPlistPath() => @"X:\z\Info.plist";

        public string GetWindowsManifestPath() => @"X:\z\Package.appxmanifest";

        public string GetCsprojPath() => csprojPath;
    }

    [Fact]
    public async Task PatchVersionAsync_bumps_ApplicationVersion_and_ApplicationDisplayVersion_in_csproj()
    {
        const string csprojPath = @"R:\fake\bible-alarm\src\Bible.Alarm\Bible.Alarm.csproj";
        var csprojSeed = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ApplicationVersion>41</ApplicationVersion>
                <ApplicationDisplayVersion>9.98</ApplicationDisplayVersion>
              </PropertyGroup>
            </Project>
            """;

        var files = new MemoryFileService();
        files.Files[csprojPath] = csprojSeed;

        var sut = new ReleaseVersionPatcher(new VersionService(), files, new StubPathService(csprojPath));

        await sut.PatchVersionAsync();

        var updated = files.Files[csprojPath];
        Assert.Contains("<ApplicationVersion>42</ApplicationVersion>", updated, StringComparison.Ordinal);
        Assert.Contains("<ApplicationDisplayVersion>9.99</ApplicationDisplayVersion>", updated, StringComparison.Ordinal);
    }
}
