using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class IOSVersionPatcher(IVersionService versionService, IFileService fileService, IPathService pathService)
    : IPlatformVersionPatcher
{
    public string PlatformName => "iOS";

    public async Task PatchVersionAsync()
    {
        var manifestFile = pathService.GetIOSInfoPlistPath();
        
        if (!fileService.FileExists(manifestFile))
        {
            Console.WriteLine($"iOS Info.plist file not found: {manifestFile}");
            return;
        }

        var content = await fileService.ReadFileAsync(manifestFile);
        var lines = content.Split('\n');
        var output = new StringBuilder();
        var versionFound = false;

        for (int i = 0; i < lines.Length; i++)
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
                        var newVersion = versionService.IncrementVersion(oldVersion);
                        var matchRegex = new Regex(@"<string>.*<\/string>");
                        var newLine = matchRegex.Replace(nextLine, $"<string>{newVersion}</string>");
                        output.AppendLine(newLine);
                        i++; // Skip the next line since we processed it
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
        }
    }

    private static string ExtractVersionFromLine(string line)
    {
        var match = Regex.Match(line, @"<string>(.*?)</string>");
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}
