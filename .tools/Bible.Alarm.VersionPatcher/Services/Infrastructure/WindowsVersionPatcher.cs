using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class WindowsVersionPatcher(IVersionService versionService, IFileService fileService, IPathService pathService)
    : IPlatformVersionPatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public string PlatformName => "Windows";

    public async Task PatchVersionAsync()
    {
        var manifestFile = pathService.GetWindowsManifestPath();

        if (!fileService.FileExists(manifestFile))
        {
            Console.WriteLine($"Windows Package.appxmanifest file not found: {manifestFile}");
            return;
        }

        var content = await fileService.ReadFileAsync(manifestFile);
        var doc = new XmlDocument();

        try
        {
            doc.LoadXml(content);
        }
        catch (XmlException)
        {
            Console.WriteLine("Invalid XML format in Windows manifest");
            return;
        }

        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

        var identityNode = doc.SelectSingleNode("//appx:Identity", namespaceManager)
                          ?? doc.SelectSingleNode("//Identity");

        if (identityNode?.Attributes == null)
        {
            Console.WriteLine("Could not find Identity node in Windows manifest");
            return;
        }

        var attrs = identityNode.Attributes;
        var versionName = attrs["Version"]?.Value;

        if (string.IsNullOrEmpty(versionName))
        {
            Console.WriteLine("Could not find Version attribute in Windows manifest");
            return;
        }

        // Store requirement: manifest Version must have revision (4th component) = 0 (e.g. 2.1.1.0, not 2.1.0.1)
        var versionParts = versionName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (versionParts.Length < 2)
        {
            Console.WriteLine("Windows version format is invalid");
            return;
        }

        var majorMinorVersion = $"{versionParts[0]}.{versionParts[1]}";
        var newMajorMinorVersion = versionService.IncrementVersion(majorMinorVersion);
        var newVersionName = $"{newMajorMinorVersion}.0.0";

        if (!newVersionName.EndsWith(".0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Windows Store requires manifest Version revision (4th component) to be 0. Got: {newVersionName}");
        }

        attrs["Version"].Value = newVersionName;

        var updatedContent = doc.OuterXml;
        await fileService.WriteFileAsync(manifestFile, updatedContent);

        Console.WriteLine($"Windows version updated: {versionName} -> {newVersionName}");

        await PatchCsprojForWindowsAsync(newMajorMinorVersion);
    }

    private async Task PatchCsprojForWindowsAsync(string newDisplayVersion)
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
}
