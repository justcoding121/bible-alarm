using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Bible.Alarm.Shared.Utilities;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class AndroidVersionPatcher : IPlatformVersionPatcher
{
    private readonly IVersionService _versionService;
    private readonly IFileService _fileService;

    public string PlatformName => "Android";

    public AndroidVersionPatcher(IVersionService versionService, IFileService fileService)
    {
        _versionService = versionService;
        _fileService = fileService;
    }

    public async Task PatchVersionAsync()
    {
        var manifestFile = Path.Combine(DirectoryHelper.IndexDirectory, "src", "Bible.Alarm", "Bible.Alarm.Droid", "Properties", "AndroidManifest.xml");
        
        if (!_fileService.FileExists(manifestFile))
        {
            Console.WriteLine($"Android manifest file not found: {manifestFile}");
            return;
        }

        var content = await _fileService.ReadFileAsync(manifestFile);
        var doc = new XmlDocument();
        doc.LoadXml(content);

        var manifestNode = doc.SelectSingleNode("/manifest");
        if (manifestNode?.Attributes == null)
        {
            Console.WriteLine("Could not find manifest node in Android manifest");
            return;
        }

        var attrs = manifestNode.Attributes;
        var versionCode = attrs["android:versionCode"]?.Value;
        var versionName = attrs["android:versionName"]?.Value;

        if (string.IsNullOrEmpty(versionCode) || string.IsNullOrEmpty(versionName))
        {
            Console.WriteLine("Could not find version attributes in Android manifest");
            return;
        }

        var newVersionCode = _versionService.IncrementVersionCode(versionCode);
        var newVersionName = _versionService.IncrementVersion(versionName);

        attrs["android:versionCode"].Value = newVersionCode;
        attrs["android:versionName"].Value = newVersionName;

        var updatedContent = doc.OuterXml;
        await _fileService.WriteFileAsync(manifestFile, updatedContent);

        Console.WriteLine($"Android version updated: {versionName} -> {newVersionName}, {versionCode} -> {newVersionCode}");
    }
}
