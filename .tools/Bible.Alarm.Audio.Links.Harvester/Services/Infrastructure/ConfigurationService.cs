using System;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class ConfigurationService : IConfigurationService
{
    public string GetValue(string key)
    {
        return Environment.GetEnvironmentVariable(key) ?? throw new InvalidOperationException($"Configuration key '{key}' not found.");
    }

    public T GetValue<T>(string key)
    {
        var value = GetValue(key);
        return (T)Convert.ChangeType(value, typeof(T));
    }

    public bool HasValue(string key)
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key));
    }
}
