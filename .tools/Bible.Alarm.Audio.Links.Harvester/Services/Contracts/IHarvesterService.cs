using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IHarvesterService
{
    Task<ConcurrentDictionary<string, string>> HarvestLanguageMappingsAsync();
    Task<ConcurrentDictionary<string, List<string>>> HarvestLanguageEditionsAsync();
}
