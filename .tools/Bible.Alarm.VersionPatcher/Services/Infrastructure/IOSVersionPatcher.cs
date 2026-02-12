using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public partial class IosVersionPatcher(IVersionService versionService, IFileService fileService, IPathService pathService)
    : IPlatformVersionPatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public string PlatformName => "iOS";

    public async Task PatchVersionAsync()
    {
        var manifestFile = pathService.GetIosInfoPlistPath();

        if (!fileService.FileExists(manifestFile))
        {
            Console.WriteLine($"iOS Info.plist file not found: {manifestFile}");
            return;
        }

        var content = await fileService.ReadFileAsync(manifestFile);
        var lines = content.Split('\n');
        var output = new StringBuilder();
        var versionFound = false;
        string? newVersion = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.Trim() == "<key>CFBundleVersion</key>")
            {
                output.AppendLine(line);

                if (i + 1 < lines.Length)
                {
                    var nextLine = lines[i + 1];
                    var oldVersion = ExtractVersionFromLine(nextLine);

                    if (!string.IsNullOrEmpty(oldVersion))
                    {
                        newVersion = versionService.IncrementVersion(oldVersion);
                        var matchRegex = MatchRegexGenerated();
                        var newLine = matchRegex.Replace(nextLine, $"<string>{newVersion}</string>");
                        output.AppendLine(newLine);
                        i++;
                        versionFound = true;

                        Console.WriteLine($"iOS version updated: {oldVersion} -> {newVersion}");
                    }
                    else
                    {
                        output.AppendLine(nextLine);
                        i++;
                    }
                }
                else
                {
                    output.AppendLine(line);
                }
            }
            else
            {
                output.AppendLine(line);
            }
        }

        if (versionFound)
        {
            await fileService.WriteFileAsync(manifestFile, output.ToString());
            if (!string.IsNullOrEmpty(newVersion))
                await PatchCsprojForIosAsync(newVersion);
        }
    }

    private async Task PatchCsprojForIosAsync(string newDisplayVersion)
    {
        var csprojFile = pathService.GetCsprojPath();
        if (!fileService.FileExists(csprojFile))
            return;

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
            Console.WriteLine($"ApplicationVersion updated: {currentVersionCode} -> {newVersionCode}");
        }

        content = Regex.Replace(content,
            @"<ApplicationDisplayVersion>[\d.]+</ApplicationDisplayVersion>",
            $"<ApplicationDisplayVersion>{newDisplayVersion}</ApplicationDisplayVersion>",
            RegexOptions.None, RegexTimeout);
        Console.WriteLine($"ApplicationDisplayVersion updated -> {newDisplayVersion}");

        await fileService.WriteFileAsync(csprojFile, content);
    }

    private static string ExtractVersionFromLine(string line)
    {
        var match = ExtractVersionRegex().Match(line);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    [GeneratedRegex(@"<string>.*<\/string>")]
    private static partial Regex MatchRegexGenerated();
    [GeneratedRegex(@"<string>(.*?)</string>")]
    private static partial Regex ExtractVersionRegex();
}
