using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Bible.Alarm.Audio.Links.Harvestor.Harvestors.Bible;
using Bible.Alarm.Audio.Links.Harvestor.Harvestors.Music;
using Bible.Alarm.Audio.Links.Harvestor.Models;
using Bible.Alarm.Audio.Links.Harvestor.Utility;
using Bible.Alarm.Shared.Helpers;
using System.Text.Json;
using DirectoryHelper = Bible.Alarm.Audio.Links.Harvestor.Utility.DirectoryHelper;

namespace Bible.Alarm.Audio.Links.Harvestor
{
    public class Program
    {

        private static readonly Dictionary<string, string> _biblePublicationCodeToNameMappings =
            JwSourceHelper.PublicationCodeToNameMappings;


        /// <summary>
        /// Harvest URL links to get the mp3 files liks for Bible & Music 
        /// </summary>
        /// <param name="args"></param>
        public static async Task Main(string[] args)
        {
            bool isTestRun = args.Contains("--TestRun", StringComparer.OrdinalIgnoreCase);
            
            if (isTestRun)
            {
                Console.WriteLine("=== TEST RUN MODE: Processing at most 2 languages per publication ===");
            }

            try
            {
                var originalIndexFileSize =
                    (new FileInfo($"{DirectoryHelper.IndexDirectory}/index.zip")).Length;

                deleteDirectory(DirectoryHelper.IndexDirectory);

                var bibleTasks = new List<Task>();

                var languageCodeToNameMappings = new ConcurrentDictionary<string, string>();
                var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>();

                //////Bible
                bibleTasks.Add(JwBibleHarvester.Harvest_Bible_Links(JwSourceHelper.PublicationCodeToNameMappings, languageCodeToNameMappings, languageCodeToEditionsMapping, isTestRun));
                //bibleTasks.Add(BgBibleHarvester.Harvest_Bible_Links(BgSourceHelper.PublicationCodeToNameMappings, languageCodeToNameMappings, languageCodeToEditionsMapping));

                var musicTasks = new List<Task>
                {
                    ////////Music
                    MusicHarverster.Harvest_Vocal_Music_Links(isTestRun),
                    MusicHarverster.Harvest_Music_Melody_Links(isTestRun)
                };

                await Task.WhenAll(bibleTasks.Concat(musicTasks).ToArray());

                writeBibleIndex(languageCodeToNameMappings, languageCodeToEditionsMapping);

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

                await DbSeeder.Seed($"{DirectoryHelper.IndexDirectory}");

                // Wait and retry to ensure database file is fully released
                await RetryZipFiles();

                var newIndexFileSize =
                    (new FileInfo($"{DirectoryHelper.IndexDirectory}/index.zip")).Length;

                Console.WriteLine("Old size:" + (originalIndexFileSize / 1024) + "kb");
                Console.WriteLine("New size:" + (newIndexFileSize / 1024) + "kb");

                if (!isTestRun && Math.Abs(originalIndexFileSize - newIndexFileSize) > (1024 * 700))
                {
                    throw new ApplicationException("New index file size is strangely smaller than old index file size.");
                }

                if (!isTestRun)
                {
                    await publishToCloudFront();
                }
                else
                {
                    Console.WriteLine("=== TEST RUN: Skipping cloud publishing ===");
                }
            }
            finally
            {
                var zipIndex = $"{DirectoryHelper.IndexDirectory}/index.zip";
                if (!File.Exists(zipIndex))
                {
                    throw new Exception("Harvesting failed to create zip file.");
                }
            }
        }

        private static void writeBibleIndex(ConcurrentDictionary<string, string> languageCodeToNameMappings,
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
                    Name = _biblePublicationCodeToNameMappings[x]
                }).OrderBy(x => x.Code)));
            }

        }


        private static async Task RetryZipFiles(int maxRetries = 10, int delayMs = 2000)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        Console.WriteLine($"Retrying zip operation (attempt {attempt + 1}/{maxRetries}) after {delayMs}ms delay...");
                        await Task.Delay(delayMs);
                    }

                    ZipFiles();
                    return; // Success
                }
                catch (System.IO.IOException ex) when (attempt < maxRetries - 1 && ex.Message.Contains("being used by another process"))
                {
                    // Continue to retry
                    Console.WriteLine($"Database file is still locked, will retry...");
                }
            }

            // If we get here, all retries failed - try copying to temp location first
            Console.WriteLine("All retries failed. Attempting to copy database to temporary location before zipping...");
            await ZipFilesWithCopy();
        }

        private static async Task ZipFilesWithCopy()
        {
            var dbDir = Path.Combine(DirectoryHelper.IndexDirectory, "db");
            var tempDbDir = Path.Combine(DirectoryHelper.IndexDirectory, "db_temp");
            var zipIndex = $"{DirectoryHelper.IndexDirectory}/index.zip";

            try
            {
                // Delete temp directory if it exists
                if (Directory.Exists(tempDbDir))
                {
                    Directory.Delete(tempDbDir, true);
                }

                // Create temp directory
                Directory.CreateDirectory(tempDbDir);

                // Copy all files from db to db_temp
                foreach (var file in Directory.GetFiles(dbDir))
                {
                    var fileName = Path.GetFileName(file);
                    var destFile = Path.Combine(tempDbDir, fileName);
                    
                    // Retry copying the database file if it's locked
                    var maxCopyRetries = 5;
                    for (int i = 0; i < maxCopyRetries; i++)
                    {
                        try
                        {
                            File.Copy(file, destFile, true);
                            break; // Success
                        }
                        catch (System.IO.IOException) when (i < maxCopyRetries - 1 && fileName == "mediaIndex.db")
                        {
                            Console.WriteLine($"Database file is locked, retrying copy (attempt {i + 1}/{maxCopyRetries})...");
                            await Task.Delay(1000);
                        }
                    }
                }

                // Now zip from the temp directory
                if (File.Exists(zipIndex))
                {
                    File.Delete(zipIndex);
                }

                ZipFile.CreateFromDirectory(tempDbDir, zipIndex);
                Console.WriteLine("Successfully zipped database from temporary copy.");
            }
            finally
            {
                // Clean up temp directory
                if (Directory.Exists(tempDbDir))
                {
                    try
                    {
                        Directory.Delete(tempDbDir, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
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

        /// <summary>
        /// Depth-first recursive delete, with handling for descendant 
        /// directories open in Windows Explorer.
        /// </summary>
        private static void deleteDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (var directory in Directory.GetDirectories(path))
            {
                deleteDirectory(directory);
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

        private async static Task publishToCloudFront()
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
            var fileName = $"{utcTime.Day}-{utcTime.Month}-{utcTime.Year}.zip";
            var keyName = $"{keyPrefix}/{fileName}";
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = keyName,
                FilePath = $"{DirectoryHelper.IndexDirectory}/index.zip",

            });

            if (listObjectsResponse.S3Objects.Count > 0)
            {
                var deleteObjectsRequest = new DeleteObjectsRequest
                {
                    BucketName = bucketName
                };

                listObjectsResponse.S3Objects.ForEach(x =>
                {
                    if (x.Key != keyName)
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
}
