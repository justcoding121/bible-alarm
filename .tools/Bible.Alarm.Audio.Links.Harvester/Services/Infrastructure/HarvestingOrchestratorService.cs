using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Shared.Utilities;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class HarvestingOrchestratorService : IHarvestingOrchestratorService
{
    private readonly IBibleHarvesterService _bibleHarvesterService;
    private readonly IMusicHarvesterService _musicHarvesterService;
    private readonly IIndexService _indexService;
    private readonly ICloudPublishingService _cloudPublishingService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IConfigurationService _configurationService;

    private static readonly Dictionary<string, string> BiblePublicationCodeToNameMappings =
        JwSourceHelper.PublicationCodeToNameMappings.ToDictionary(x => x.Key, x => x.Value);

    public HarvestingOrchestratorService(
        IBibleHarvesterService bibleHarvesterService,
        IMusicHarvesterService musicHarvesterService,
        IIndexService indexService,
        ICloudPublishingService cloudPublishingService,
        IFileSystemService fileSystemService,
        IConfigurationService configurationService)
    {
        _bibleHarvesterService = bibleHarvesterService;
        _musicHarvesterService = musicHarvesterService;
        _indexService = indexService;
        _cloudPublishingService = cloudPublishingService;
        _fileSystemService = fileSystemService;
        _configurationService = configurationService;
    }

    public async Task ExecuteHarvestingAsync()
    {
        try
        {
            var originalIndexFileSize = new FileInfo($"{DirectoryHelper.IndexDirectory}/index.zip").Length;

            await _fileSystemService.CleanupIndexDirectoryAsync();

            var languageCodeToNameMappings = new ConcurrentDictionary<string, string>();
            var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>();

            // Execute Bible and Music harvesting in parallel
            var bibleTasks = new List<Task>
            {
                _bibleHarvesterService.HarvestBibleLinksAsync(
                    BiblePublicationCodeToNameMappings,
                    languageCodeToNameMappings,
                    languageCodeToEditionsMapping)
            };

            var musicTasks = new List<Task>
            {
                _musicHarvesterService.HarvestVocalMusicLinksAsync(),
                _musicHarvesterService.HarvestMelodyMusicLinksAsync()
            };

            await Task.WhenAll(bibleTasks.Concat(musicTasks).ToArray());

            // Create index files
            await _indexService.WriteBibleIndexAsync(languageCodeToNameMappings, languageCodeToEditionsMapping);
            await _indexService.CreateIndexMetadataAsync();

            // Zip the files
            await _indexService.ZipIndexFilesAsync();

            var newIndexFileSize = new FileInfo($"{DirectoryHelper.IndexDirectory}/index.zip").Length;

            Console.WriteLine("Old size:" + originalIndexFileSize / 1024 + "kb");
            Console.WriteLine("New size:" + newIndexFileSize / 1024 + "kb");

            if (Math.Abs(originalIndexFileSize - newIndexFileSize) > 1024 * 700)
                throw new ApplicationException("New index file size is strangely smaller than old index file size.");

            await _cloudPublishingService.PublishToCloudFrontAsync();
        }
        finally
        {
            var zipIndex = $"{DirectoryHelper.IndexDirectory}/index.zip";
            if (!await _fileSystemService.ValidateIndexFileAsync())
                throw new Exception("Harvesting failed to create zip file.");
        }
    }
}
