using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IIndexService
{
    Task WriteBibleIndexAsync(
        ConcurrentDictionary<string, string> languageCodeToNameMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping);
    
    Task CreateIndexMetadataAsync();
    Task ZipIndexFilesAsync();
    Task WriteIndexMetadataAsync();
    Task ZipIndexFiles();
}
