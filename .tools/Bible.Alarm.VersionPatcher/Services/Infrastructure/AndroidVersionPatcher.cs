using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class AndroidVersionPatcher(IVersionService versionService, IFileService fileService, IPathService pathService)
    : IPlatformVersionPatcher
{
    public string PlatformName => "Android";

    public async Task PatchVersionAsync()
    {
        var manifestFile = pathService.GetAndroidManifestPath();
        
        if (!fileService.FileExists(manifestFile))
        {
            Console.WriteLine($"Android manifest file not found: {manifestFile}");
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
            Console.WriteLine("Invalid XML format in Android manifest");
            return;
        }

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

        var newVersionCode = versionService.IncrementVersionCode(versionCode);
        var newVersionName = versionService.IncrementVersion(versionName);

        attrs["android:versionCode"].Value = newVersionCode;
        attrs["android:versionName"].Value = newVersionName;

        var updatedContent = doc.OuterXml;
        await fileService.WriteFileAsync(manifestFile, updatedContent);

        Console.WriteLine($"Android version updated: {versionName} -> {newVersionName}, {versionCode} -> {newVersionCode}");
    }
}
