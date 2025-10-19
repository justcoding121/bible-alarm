using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Bible.Alarm.Shared.Utilities;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class WindowsVersionPatcher : IPlatformVersionPatcher
{
    private readonly IVersionService _versionService;
    private readonly IFileService _fileService;

    public string PlatformName => "Windows";

    public WindowsVersionPatcher(IVersionService versionService, IFileService fileService)
    {
        _versionService = versionService;
        _fileService = fileService;
    }

    public async Task PatchVersionAsync()
    {
        var manifestFile = Path.Combine(DirectoryHelper.IndexDirectory, "src", "Bible.Alarm", "Platforms", "Windows", "Package.appxmanifest");
        
        if (!_fileService.FileExists(manifestFile))
        {
            Console.WriteLine($"Windows Package.appxmanifest file not found: {manifestFile}");
            return;
        }

        var content = await _fileService.ReadFileAsync(manifestFile);
        var doc = new XmlDocument();
        doc.LoadXml(content);

        var identityNode = doc.SelectSingleNode("/Package/Identity");
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

        // Extract major.minor from the version (e.g., "1.2.3.4" -> "1.2")
        var versionParts = versionName.Split('.');
        if (versionParts.Length < 2)
        {
            Console.WriteLine("Windows version format is invalid");
            return;
        }

        var majorMinorVersion = $"{versionParts[0]}.{versionParts[1]}";
        var newMajorMinorVersion = _versionService.IncrementVersion(majorMinorVersion);
        var newVersionName = $"{newMajorMinorVersion}.0.0";

        attrs["Version"].Value = newVersionName;

        var updatedContent = doc.OuterXml;
        await _fileService.WriteFileAsync(manifestFile, updatedContent);

        Console.WriteLine($"Windows version updated: {versionName} -> {newVersionName}");
    }
}
