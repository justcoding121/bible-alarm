using System;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class VersionService : IVersionService
{
    public string IncrementVersion(string currentVersion)
    {
        var versionParts = currentVersion.Split('.');
        
        if (versionParts.Length < 2)
            throw new ArgumentException("Version must have at least major.minor format", nameof(currentVersion));

        var major = int.Parse(versionParts[0]);
        var minor = int.Parse(versionParts[1]);

        var newMinor = minor == 99 ? 0 : minor + 1;
        var newMajor = newMinor == 0 ? major + 1 : major;

        return $"{newMajor}.{newMinor}";
    }

    public string IncrementVersionCode(string currentVersionCode)
    {
        if (int.TryParse(currentVersionCode, out var versionCode))
        {
            return (versionCode + 1).ToString();
        }
        
        throw new ArgumentException("Version code must be a valid integer", nameof(currentVersionCode));
    }
}
