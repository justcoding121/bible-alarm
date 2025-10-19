namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IConfigurationService
{
    string GetValue(string key);
    T GetValue<T>(string key);
    bool HasValue(string key);
}
