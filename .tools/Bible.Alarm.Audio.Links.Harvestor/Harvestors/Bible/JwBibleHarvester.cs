using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvestor.Models.Bible;
using Bible.Alarm.Audio.Links.Harvestor.Utility;
using Bible.Alarm.Shared.Constants;
using System.Text.Json;

namespace Bible.Alarm.Audio.Links.Harvestor.Harvestors.Bible
{
    internal class JwBibleHarvester
    {

        internal async static
            Task Harvest_Bible_Links(Dictionary<string, string> biblePublicationCodeToNameMappings,
                                    ConcurrentDictionary<string, string> languageCodeToNameMappings,
                                    ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping)
        {

            foreach (var publication in biblePublicationCodeToNameMappings)
            {
                var publicationCode = publication.Key;

                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?booknum=0&output=json&pub={publicationCode}&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";

                var jsonString = await DownloadUtility.GetAsync(harvestLink);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                
                // Check if root is an object, not an array
                if (root.ValueKind != JsonValueKind.Object)
                {
                    Console.WriteLine($"Warning: Root element is not an object (type: {root.ValueKind}) for publication {publicationCode}. Skipping.");
                    continue;
                }
                
                if (!root.TryGetProperty("languages", out var languages))
                {
                    Console.WriteLine($"Warning: 'languages' property not found in response for publication {publicationCode}. Skipping.");
                    continue;
                }

                foreach (var item in languages.EnumerateObject())
                {
                    var languageCode = item.Name;
                    
                    if (!item.Value.TryGetProperty("name", out var nameElement))
                    {
                        Console.WriteLine($"Warning: 'name' property not found for language {languageCode}. Skipping.");
                        continue;
                    }
                    
                    var language = nameElement.GetString();
                    if (string.IsNullOrEmpty(language))
                    {
                        Console.WriteLine($"Warning: Language name is null or empty for language {languageCode}. Skipping.");
                        continue;
                    }

                    languageCodeToNameMappings.TryAdd(languageCode, language);

                    Console.WriteLine($"Harvesting Bible chapter links for {publication.Value} of {language} language.");
                    await harvestBibleLinks(languageCode, publicationCode);

                    if (!languageCodeToEditionsMapping.TryAdd(languageCode, new List<string>([publication.Key])))
                    {
                        languageCodeToEditionsMapping[languageCode].Add(publication.Key);
                    }

                }
            }

        }

        private static async Task<bool> harvestBibleLinks(string languageCode, string publicationCode)
        {
            var booksDirectory = $"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageCode}/{publicationCode}";
            var booksIndex = $"{booksDirectory}/books.json";

            var bookNumberBookMap = new Dictionary<int, BibleBook>();
            var bookNumberChapterMap = new Dictionary<int, Dictionary<int, BibleChapter>>();

            var bookNumber = 1;

            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";

            while (bookNumber <= 66)
            {
                var jsonString = await DownloadUtility.GetAsync(harvestLink);

                JsonDocument doc = null;
                JsonElement files = default;

                try
                {
                    doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;
                    
                    // Check if root is an object, not an array
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        Console.WriteLine($"Warning: Root element is not an object (type: {root.ValueKind}) for book {bookNumber}, language {languageCode}. Skipping.");
                        doc?.Dispose();
                        bookNumber++;
                        harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
                        continue;
                    }
                    
                    if (!root.TryGetProperty("files", out files))
                    {
                        Console.WriteLine($"Warning: 'files' property not found in response for book {bookNumber}, language {languageCode}. Skipping.");
                        doc?.Dispose();
                        bookNumber++;
                        harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
                        continue;
                    }
                }
                catch (Exception e)
                {
                    if (e is JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException)
                    {
                        Console.WriteLine($"Warning: Error parsing JSON for book {bookNumber}, language {languageCode}: {e.Message}. Skipping.");
                        doc?.Dispose();
                        bookNumber++;
                        harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";

                        continue;
                    }
                    throw;
                }

                try
                {
                    if (!files.TryGetProperty(languageCode, out var languageFiles))
                    {
                        Console.WriteLine($"Warning: Language '{languageCode}' not found in files. Skipping book {bookNumber}.");
                        bookNumber++;
                        harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
                        continue;
                    }

                    if (!languageFiles.TryGetProperty("MP3", out var bookFiles))
                    {
                        Console.WriteLine($"Warning: 'MP3' property not found for language '{languageCode}'. Skipping book {bookNumber}.");
                        bookNumber++;
                        harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
                        continue;
                    }

                    foreach (var bookFile in bookFiles.EnumerateArray())
                    {
                        if (!bookFile.TryGetProperty("file", out var fileElement) || 
                            !fileElement.TryGetProperty("url", out var urlElement))
                        {
                            Console.WriteLine($"Warning: Missing 'file.url' property in book file. Skipping.");
                            continue;
                        }

                        string url = urlElement.GetString();
                        if (string.IsNullOrEmpty(url))
                        {
                            Console.WriteLine($"Warning: URL is null or empty. Skipping.");
                            continue;
                        }

                        if (!bookFile.TryGetProperty("track", out var trackElement))
                        {
                            Console.WriteLine($"Warning: Missing 'track' property in book file. Skipping.");
                            continue;
                        }

                        var track = trackElement.GetInt32();

                        if (track == 0 || url.EndsWith(".zip")) continue;

                        if (!bookFile.TryGetProperty("booknum", out var bookNumElement))
                        {
                            Console.WriteLine($"Warning: Missing 'booknum' property in book file. Skipping.");
                            continue;
                        }

                        bookNumber = bookNumElement.GetInt32();

                        if (!bookNumberBookMap.ContainsKey(bookNumber))
                        {
                            string name;
                            if (harvestLink.Contains("booknum="))
                            {
                                if (!doc.RootElement.TryGetProperty("pubName", out var pubNameElement))
                                {
                                    Console.WriteLine($"Warning: Missing 'pubName' property. Skipping book {bookNumber}.");
                                    continue;
                                }
                                name = pubNameElement.GetString()!;
                            }
                            else
                            {
                                if (!bookFile.TryGetProperty("title", out var titleElement))
                                {
                                    Console.WriteLine($"Warning: Missing 'title' property. Skipping book {bookNumber}.");
                                    continue;
                                }
                                name = titleElement.GetString()!.Split('-')[0].Trim();
                            }
                            name = name == "Psalm 1" ? "Psalms" : name;
                            bookNumberBookMap[bookNumber] = new BibleBook
                            {
                                Number = bookNumber,
                                Name = name
                            };
                        }

                        var trackNumber = track;
                        
                        if (!bookFile.TryGetProperty("duration", out var durationElement))
                        {
                            Console.WriteLine($"Warning: Missing 'duration' property. Using default value.");
                        }
                        var duration = durationElement.ValueKind != JsonValueKind.Undefined ? durationElement.GetDouble() : 0.0;
                        
                        if (!bookNumberChapterMap.ContainsKey(bookNumber))
                        {
                            bookNumberChapterMap[bookNumber] = new Dictionary<int, BibleChapter>();
                        }

                        if (!bookNumberChapterMap[bookNumber].ContainsKey(trackNumber))
                        {
                            bookNumberChapterMap[bookNumber].Add(trackNumber,
                            new BibleChapter
                            {
                                Number = trackNumber,
                                Url = url,
                            });
                        }
                    }
                }
                catch (KeyNotFoundException ex)
                {
                    Console.WriteLine($"Warning: KeyNotFoundException in book processing: {ex.Message}. Skipping book {bookNumber}.");
                    bookNumber++;
                    harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
                    continue;
                }
                finally
                {
                    doc?.Dispose();
                }

                if (harvestLink.Contains("booknum="))
                {
                    bookNumber++;
                }

                harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
            }

            if (bookNumberBookMap.Count > 0)
            {
                if (!Directory.Exists(booksDirectory))
                {
                    Directory.CreateDirectory(booksDirectory);
                }

                File.WriteAllText(booksIndex, JsonSerializer.Serialize(bookNumberBookMap.Select(x =>
                new BibleBook
                {
                    Number = x.Key,
                    Name = x.Value.Name
                }).OrderBy(x => x.Number)));

                foreach (var book in bookNumberBookMap)
                {
                    var directory = $"{booksDirectory}/{book.Value.Number}";
                    DirectoryHelper.Ensure(directory);

                    var chapterIndex = $"{directory}/chapters.json";
                    File.WriteAllText(chapterIndex, JsonSerializer.Serialize(
                    bookNumberChapterMap[book.Key]
                    .Select(x => x.Value)
                    .OrderBy(x => x.Number)
                    .ToList()));
                }

                return true;

            }

            return false;
        }

    }
}
