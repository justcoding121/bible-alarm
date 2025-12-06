namespace Bible.Alarm.Common.Interfaces.Platform;

public interface IVersionFinder : IDisposable
{
    string GetVersionName();
}