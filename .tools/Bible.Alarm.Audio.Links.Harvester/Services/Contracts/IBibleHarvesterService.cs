using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IBibleHarvesterService
{
    Task HarvestBibleLinksAsync(
        Dictionary<string, string> publicationCodeToNameMappings,
        ConcurrentDictionary<string, string> languageCodeToNameMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping);
}
