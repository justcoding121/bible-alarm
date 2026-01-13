namespace Bible.Alarm.VersionPatcher.Services.Contracts;

public interface IPathService
{
    string GetAndroidManifestPath();
    string GetIosInfoPlistPath();
    string GetWindowsManifestPath();
    string GetCsprojPath();
}
