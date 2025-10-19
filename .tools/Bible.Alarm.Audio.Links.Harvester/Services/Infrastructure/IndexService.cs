using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Shared.Models;
using Bible.Alarm.Shared.Utilities;
using Newtonsoft.Json;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class IndexService : IIndexService
{
    private readonly Dictionary<string, string> _biblePublicationCodeToNameMappings;

    public IndexService()
    {
        _biblePublicationCodeToNameMappings = JwSourceHelper.PublicationCodeToNameMappings
            .ToDictionary(x => x.Key, x => x.Value);
    }

    public async Task WriteBibleIndexAsync(
        ConcurrentDictionary<string, string> languageCodeToNameMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping)
    {
        if (!Directory.Exists($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible"))
            Directory.CreateDirectory($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible");

        await File.WriteAllTextAsync($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/languages.json",
            JsonConvert.SerializeObject(
                languageCodeToEditionsMapping.Select(x =>
                    new Language
                    {
                        Code = x.Key,
                        Name = languageCodeToNameMappings[x.Key]
                    }).OrderBy(x => x.Code).ToList()));

        foreach (var languageEditionsMap in languageCodeToEditionsMapping)
        {
            if (!Directory.Exists($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageEditionsMap.Key}"))
                Directory.CreateDirectory($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageEditionsMap.Key}");

            await File.WriteAllTextAsync(
                $"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageEditionsMap.Key}/publications.json",
                JsonConvert.SerializeObject(
                    languageEditionsMap.Value.Select(x =>
                        new Publication
                        {
                            Code = x,
                            Name = _biblePublicationCodeToNameMappings[x]
                        }).OrderBy(x => x.Code)));
        }
    }

    public async Task CreateIndexMetadataAsync()
    {
        var index = new
        {
            ReleaseDate = DateTime.Now.Ticks
        };

        var indexFile = $"{DirectoryHelper.IndexDirectory}/media/index.json";
        if (File.Exists(indexFile)) File.Delete(indexFile);

        await File.WriteAllTextAsync(indexFile, JsonConvert.SerializeObject(index));
    }

    public async Task ZipIndexFilesAsync()
    {
        var zipIndex = $"{DirectoryHelper.IndexDirectory}/index.zip";
        if (File.Exists(zipIndex)) File.Delete(zipIndex);

        await Task.Run(() => ZipFile.CreateFromDirectory($"{Path.Combine(DirectoryHelper.IndexDirectory, "db")}", zipIndex));
    }
}
