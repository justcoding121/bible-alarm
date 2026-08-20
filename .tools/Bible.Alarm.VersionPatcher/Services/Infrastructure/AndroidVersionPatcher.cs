using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class AndroidVersionPatcher(IVersionService versionService, IFileService fileService, IPathService pathService)
    : IPlatformVersionPatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public string PlatformName => "Android";

    public async Task PatchVersionAsync()
    {
        // In .NET MAUI, Android versions are stored in the csproj file, not AndroidManifest.xml
        var csprojFile = pathService.GetCsprojPath();

        if (!fileService.FileExists(csprojFile))
        {
            Console.WriteLine($"Android csproj file not found: {csprojFile}");
            return;
        }

        var content = await fileService.ReadFileAsync(csprojFile);

        var versionCodeMatch = Regex.Match(content, @"<ApplicationVersion>(\d+)</ApplicationVersion>", RegexOptions.None, RegexTimeout);
        if (versionCodeMatch.Success)
        {
            var currentVersionCode = versionCodeMatch.Groups[1].Value;
            var newVersionCode = versionService.IncrementVersionCode(currentVersionCode);
            content = Regex.Replace(content,
                @"<ApplicationVersion>\d+</ApplicationVersion>",
                $"<ApplicationVersion>{newVersionCode}</ApplicationVersion>",
                RegexOptions.None, RegexTimeout);
            Console.WriteLine($"Android ApplicationVersion updated: {currentVersionCode} -> {newVersionCode}");
        }
        else
        {
            Console.WriteLine("Could not find ApplicationVersion in csproj");
        }

        var versionNameMatch = Regex.Match(content, @"<ApplicationDisplayVersion>([\d.]+)</ApplicationDisplayVersion>", RegexOptions.None, RegexTimeout);
        if (versionNameMatch.Success)
        {
            var currentVersionName = versionNameMatch.Groups[1].Value;
            var newVersionName = versionService.IncrementVersion(currentVersionName);
            content = Regex.Replace(content,
                @"<ApplicationDisplayVersion>[\d.]+</ApplicationDisplayVersion>",
                $"<ApplicationDisplayVersion>{newVersionName}</ApplicationDisplayVersion>",
                RegexOptions.None, RegexTimeout);
            Console.WriteLine($"Android ApplicationDisplayVersion updated: {currentVersionName} -> {newVersionName}");
        }
        else
        {
            Console.WriteLine("Could not find ApplicationDisplayVersion in csproj");
        }

        await fileService.WriteFileAsync(csprojFile, content);
    }
}
