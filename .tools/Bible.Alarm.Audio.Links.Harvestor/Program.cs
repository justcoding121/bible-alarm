using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using AudioLinkHarvester.Audio;
using AudioLinkHarvester.Bible;
using AudioLinkHarvester.Models;
using AudioLinkHarvestor.Utility;
using Bible.Alarm.Audio.Links.Harvestor;
using Bible.Alarm.Common.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace AudioLinkHarvester
{
    public class Program
    {

        private static readonly Dictionary<string, string> _biblePublicationCodeToNameMappings =
            JwSourceHelper.PublicationCodeToNameMappings.Select(x => x)
                    .ToDictionary(x => x.Key, x => x.Value);


        /// <summary>
        /// Harvest URL links to get the mp3 files liks for Bible & Music 
        /// </summary>
        /// <param name="args"></param>
        public static async Task Main(string[] args)
        {
            try
            {
                var originalIndexFileSize =
                    (new FileInfo($"{DirectoryHelper.IndexDirectory}/index.zip")).Length;

                deleteDirectory(DirectoryHelper.IndexDirectory);

                var bibleTasks = new List<Task>();

                var languageCodeToNameMappings = new ConcurrentDictionary<string, string>();
                var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>();

                //////Bible
                bibleTasks.Add(JwBibleHarvester.Harvest_Bible_Links(JwSourceHelper.PublicationCodeToNameMappings, languageCodeToNameMappings, languageCodeToEditionsMapping));
                //bibleTasks.Add(BgBibleHarvester.Harvest_Bible_Links(BgSourceHelper.PublicationCodeToNameMappings, languageCodeToNameMappings, languageCodeToEditionsMapping));

                var musicTasks = new List<Task>
                {
                    ////////Music
                    MusicHarverster.Harvest_Vocal_Music_Links(),
                    MusicHarverster.Harvest_Music_Melody_Links()
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

                await File.WriteAllTextAsync(indexFile, JsonConvert.SerializeObject(index));

                await DbSeeder.Seed($"{DirectoryHelper.IndexDirectory}");

                ZipFiles();

                var newIndexFileSize =
                    (new FileInfo($"{DirectoryHelper.IndexDirectory}/index.zip")).Length;

                Console.WriteLine("Old size:" + (originalIndexFileSize / 1024) + "kb");
                Console.WriteLine("New size:" + (newIndexFileSize / 1024) + "kb");

                if (Math.Abs(originalIndexFileSize - newIndexFileSize) > (1024 * 700))
                {
                    throw new ApplicationException("New index file size is strangely smaller than old index file size.");
                }

                await publishToCloudFront();
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

            File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/languages.json", JsonConvert.SerializeObject(
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

                File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageEditionsMap.Key}/publications.json", JsonConvert.SerializeObject(
                languageEditionsMap.Value.Select(x =>
                new Publication
                {
                    Code = x,
                    Name = _biblePublicationCodeToNameMappings[x]
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

            foreach (string directory in Directory.GetDirectories(path))
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

            var listObjectsResponse = await s3Client.ListObjectsAsync(new ListObjectsRequest()
            {
                Prefix = $"{keyPrefix}/",
                BucketName = bucketName
            });

            var utcTime = DateTime.UtcNow;
            var fileName = $"{utcTime.Day}-{utcTime.Month}-{utcTime.Year}.zip";
            var keyName = $"{keyPrefix}/{fileName}";
            await s3Client.PutObjectAsync(new PutObjectRequest()
            {
                BucketName = bucketName,
                Key = keyName,
                FilePath = $"{DirectoryHelper.IndexDirectory}/index.zip",

            });

            if (listObjectsResponse.S3Objects.Count > 0)
            {
                var deleteObjectsRequest = new DeleteObjectsRequest()
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
