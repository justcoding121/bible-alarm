#nullable enable
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Bible.Alarm.VersionPatcher.Constants;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

/// <summary>
/// Single version bump for release: updates csproj (ApplicationVersion, ApplicationDisplayVersion)
/// and syncs iOS Info.plist and Windows Package.appxmanifest so all platforms share one version.
/// Use --platform=Release in CI so only one commit updates version per release.
/// </summary>
public class ReleaseVersionPatcher(IVersionService versionService, IFileService fileService, IPathService pathService)
    : IPlatformVersionPatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public string PlatformName => "Release";

    public async Task PatchVersionAsync()
    {
        var csprojFile = pathService.GetCsprojPath();
        if (!fileService.FileExists(csprojFile))
        {
            Console.WriteLine("Release: csproj not found");
            return;
        }

        var content = await fileService.ReadFileAsync(csprojFile);
        string? newVersionCode = null;
        string? newVersionName = null;

        var versionCodeMatch = Regex.Match(content, @"<ApplicationVersion>(\d+)</ApplicationVersion>", RegexOptions.None, RegexTimeout);
        if (versionCodeMatch.Success)
        {
            var current = versionCodeMatch.Groups[1].Value;
            newVersionCode = versionService.IncrementVersionCode(current);
            content = Regex.Replace(content,
                @"<ApplicationVersion>\d+</ApplicationVersion>",
                $"<ApplicationVersion>{newVersionCode}</ApplicationVersion>",
                RegexOptions.None, RegexTimeout);
            Console.WriteLine($"Release ApplicationVersion: {current} -> {newVersionCode}");
        }

        var versionNameMatch = Regex.Match(content, @"<ApplicationDisplayVersion>([\d.]+)</ApplicationDisplayVersion>", RegexOptions.None, RegexTimeout);
        if (versionNameMatch.Success)
        {
            var current = versionNameMatch.Groups[1].Value;
            newVersionName = versionService.IncrementVersion(current);
            content = Regex.Replace(content,
                @"<ApplicationDisplayVersion>[\d.]+</ApplicationDisplayVersion>",
                $"<ApplicationDisplayVersion>{newVersionName}</ApplicationDisplayVersion>",
                RegexOptions.None, RegexTimeout);
            Console.WriteLine($"Release ApplicationDisplayVersion: {current} -> {newVersionName}");
        }

        await fileService.WriteFileAsync(csprojFile, content);

        if (!string.IsNullOrEmpty(newVersionName))
        {
            await SyncIosManifestAsync(newVersionName);
            await SyncWindowsManifestAsync(newVersionName);
        }
    }

    private async Task SyncIosManifestAsync(string displayVersion)
    {
        var plistPath = pathService.GetIosInfoPlistPath();
        if (!fileService.FileExists(plistPath))
            return;

        var content = await fileService.ReadFileAsync(plistPath);
        content = Regex.Replace(content,
            @"(<key>CFBundleVersion</key>\s*<string>)[^<]+(</string>)",
            m => m.Groups[1].Value + displayVersion + m.Groups[2].Value,
            RegexOptions.None, RegexTimeout);
        await fileService.WriteFileAsync(plistPath, content);
        Console.WriteLine($"Release iOS Info.plist synced -> {displayVersion}");
    }

    private async Task SyncWindowsManifestAsync(string displayVersion)
    {
        var manifestPath = pathService.GetWindowsManifestPath();
        if (!fileService.FileExists(manifestPath))
            return;

        var fourPart = $"{displayVersion}.0.0";
        var content = await fileService.ReadFileAsync(manifestPath);
        var doc = new XmlDocument();
        try
        {
            doc.LoadXml(content);
        }
        catch (XmlException)
        {
            return;
        }

        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("appx", AppxManifestXml.FoundationWindows10);
        var identity = doc.SelectSingleNode("//appx:Identity", nsmgr) ?? doc.SelectSingleNode("//Identity");
        if (identity?.Attributes?["Version"] != null)
        {
            identity!.Attributes!["Version"]!.Value = fourPart;
            await fileService.WriteFileAsync(manifestPath, doc.OuterXml);
            Console.WriteLine($"Release Windows Package.appxmanifest synced -> {fourPart}");
        }
    }
}
