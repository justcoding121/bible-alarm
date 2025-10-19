namespace Bible.Alarm.VersionPatcher.Services.Contracts;

public interface IVersionService
{
    string IncrementVersion(string currentVersion);
    string IncrementVersionCode(string currentVersionCode);
}
