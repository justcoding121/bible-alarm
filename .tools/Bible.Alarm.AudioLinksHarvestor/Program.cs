using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Bible.Alarm.AudioLinksHarvestor.Harvestors.Bible;
using Bible.Alarm.AudioLinksHarvestor.Harvestors.Music;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using DirectoryHelper = Bible.Alarm.AudioLinksHarvestor.Utility.DirectoryHelper;
using ILogger = Serilog.ILogger;

namespace Bible.Alarm.AudioLinksHarvestor;

public class Program
{

    private static readonly Dictionary<string, string> biblePublicationCodeToNameMappings =
        JwSourceHelper.PublicationCodeToNameMappings;


    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddSerilog(Log.Logger);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        });
        services.AddSingleton(_ => Log.Logger);

        services.AddDbContext<MediaDbContext>(options =>
        {
            var indexDir = DirectoryHelper.IndexDirectory;
            var dbDir = Path.Combine(new DirectoryInfo(indexDir).FullName, "db");
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }
            var dbPath = Path.Combine(dbDir, "mediaIndex.db");

            var connectionString = $"Data Source={dbPath};";

            // Specify migrations assembly so EF Core can find and apply all migrations
            options.UseSqlite(connectionString, b =>
            {
                b.MigrationsAssembly("Bible.Alarm.Shared");
                b.CommandTimeout(60);
            });
        });

        services.AddTransient<JwBibleHarvester>();
        services.AddTransient<MusicHarvester>();
        services.AddTransient<DbSeeder>();
        services.AddTransient<DownloadUtility>();

        await using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger>();

        bool isTestRun = args.Contains("--TestRun", StringComparer.OrdinalIgnoreCase);

        if (isTestRun)
        {
            logger.Information("=== TEST RUN MODE: Processing only English language per publication ===");
        }

        try
        {
            var originalIndexFileSize =
                (new FileInfo($"{DirectoryHelper.IndexDirectory}/index.zip")).Length;

            DeleteDirectory(DirectoryHelper.IndexDirectory);

            var bibleTasks = new List<Task>();

            var languageCodeToNameMappings = new ConcurrentDictionary<string, string>();
            var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>();

            await using (var harvesterScope = serviceProvider.CreateAsyncScope())
            {
                var bibleHarvester = harvesterScope.ServiceProvider.GetRequiredService<JwBibleHarvester>();
                var musicHarvester = harvesterScope.ServiceProvider.GetRequiredService<MusicHarvester>();

                bibleTasks.Add(bibleHarvester.HarvestBibleLinks(JwSourceHelper.PublicationCodeToNameMappings, languageCodeToNameMappings, languageCodeToEditionsMapping, isTestRun));

                var musicTasks = new List<Task>
                {
                    musicHarvester.HarvestVocalMusicLinks(isTestRun),
                    musicHarvester.HarvestMusicMelodyLinks(isTestRun)
                };

                await Task.WhenAll([.. bibleTasks, .. musicTasks]);
            }

            WriteBibleIndex(languageCodeToNameMappings, languageCodeToEditionsMapping);

            var index = new
            {
                ReleaseDate = DateTime.Now.Ticks
            };

            var indexFile = $"{DirectoryHelper.IndexDirectory}/media/index.json";
            if (File.Exists(indexFile))
            {
                File.Delete(indexFile);
            }

            await File.WriteAllTextAsync(indexFile, JsonSerializer.Serialize(index));

            await using (var seederScope = serviceProvider.CreateAsyncScope())
            {
                var dbSeeder = seederScope.ServiceProvider.GetRequiredService<DbSeeder>();
                try
                {
                    await dbSeeder.Seed();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Seeding failed");
                    return 1;
                }
            }

            SqliteConnection.ClearAllPools();
            logger.Information("All SQLite connections closed. Safe to zip database.");

            ZipFiles();

            var newIndexFileSize =
                (new FileInfo($"{DirectoryHelper.IndexDirectory}/index.zip")).Length;

            logger.Information("Old size: {OldSize}kb", originalIndexFileSize / 1024);
            logger.Information("New size: {NewSize}kb", newIndexFileSize / 1024);

            // Only check if new size is significantly smaller (could indicate data loss)
            // Size increases are expected when new content is added
            if (!isTestRun && newIndexFileSize < originalIndexFileSize &&
                (originalIndexFileSize - newIndexFileSize) > (1024 * 1024)) // 1 MB threshold
            {
                throw new ApplicationException($"New index file size ({newIndexFileSize / 1024}kb) is significantly smaller than old index file size ({originalIndexFileSize / 1024}kb). This could indicate data loss.");
            }

            if (!isTestRun)
            {
                await PublishToCloudFront(logger);
            }
            else
            {
                logger.Information("=== TEST RUN: Skipping cloud publishing ===");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during harvesting");
            return 1;
        }
        finally
        {
            if (serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }

            Log.CloseAndFlush();
        }

        var zipIndex = $"{DirectoryHelper.IndexDirectory}/index.zip";
        if (!File.Exists(zipIndex))
        {
            logger.Error("Harvesting failed to create zip file.");
            return 1;
        }

        return 0;
    }

    private static void WriteBibleIndex(ConcurrentDictionary<string, string> languageCodeToNameMappings,
            ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping)
    {
        if (!Directory.Exists($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible"))
        {
            Directory.CreateDirectory($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible");
        }

        File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/languages.json", JsonSerializer.Serialize(
            languageCodeToEditionsMapping.Select(x =>
            new Language
            {
                Code = x.Key,
                Name = languageCodeToNameMappings[x.Key]

            }).OrderBy(x => x.Code).ToList()));

        foreach (var languageEditionsMap in languageCodeToEditionsMapping)
        {
            if (!Directory.Exists($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageEditionsMap.Key}"))
            {
                Directory.CreateDirectory($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageEditionsMap.Key}");
            }

            File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageEditionsMap.Key}/publications.json", JsonSerializer.Serialize(
            languageEditionsMap.Value.Select(x =>
            new Publication
            {
                Code = x,
                Name = biblePublicationCodeToNameMappings[x]
            }).OrderBy(x => x.Code)));
        }

    }


    private static void ZipFiles()
    {
        var zipIndex = $"{DirectoryHelper.IndexDirectory}/index.zip";
        if (File.Exists(zipIndex))
        {
            File.Delete(zipIndex);
        }

        ZipFile.CreateFromDirectory($"{Path.Combine(DirectoryHelper.IndexDirectory, "db")}", zipIndex);
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(path))
        {
            DeleteDirectory(directory);
        }

        var files = Directory.GetFiles(path);

        foreach (var file in files)
        {
            if (!file.EndsWith("index.zip"))
            {
                File.Delete(file);
            }
        }

        if (Directory.GetFiles(path).Length > 0
            || Directory.GetDirectories(path).Length > 0)
        {
            return;
        }

        try
        {
            Directory.Delete(path);
        }
        catch (IOException)
        {
            Directory.Delete(path);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(path);
        }
    }

    private static async Task PublishToCloudFront(ILogger logger)
    {
        var keyPrefix = "bible-alarm/media-index";
        var bucketName = "jthomas.info";
        using var s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName("ca-central-1"));

        var listObjectsResponse = await s3Client.ListObjectsAsync(new ListObjectsRequest
        {
            Prefix = $"{keyPrefix}/",
            BucketName = bucketName
        });

        var utcTime = DateTime.UtcNow;
        // Use new file name format with prefix (e.g., "v2-7-1-2024.zip")
        var fileName = $"{AppConstants.ApiEndpoints.MediaIndexFileNamePrefix}{utcTime.Day}-{utcTime.Month}-{utcTime.Year}.zip";
        var keyName = $"{keyPrefix}/{fileName}";
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = keyName,
            FilePath = $"{DirectoryHelper.IndexDirectory}/index.zip",

        });

        // Only delete files with the new prefix format to preserve pre-migration files
        if (listObjectsResponse.S3Objects.Count > 0)
        {
            var deleteObjectsRequest = new DeleteObjectsRequest
            {
                BucketName = bucketName
            };

            listObjectsResponse.S3Objects.ForEach(x =>
            {
                // Only delete files with the new prefix format (v2-), preserve old format files
                if (x.Key != keyName && x.Key.Contains($"{keyPrefix}/{AppConstants.ApiEndpoints.MediaIndexFileNamePrefix}"))
                {
                    deleteObjectsRequest.AddKey(x.Key);
                }
            });

            if (deleteObjectsRequest.Objects.Count > 0)
            {
                await s3Client.DeleteObjectsAsync(deleteObjectsRequest);
            }

        }

    }
}
