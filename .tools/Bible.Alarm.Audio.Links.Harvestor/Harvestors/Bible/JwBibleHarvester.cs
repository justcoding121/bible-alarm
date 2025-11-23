using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvestor.Models.Bible;
using Bible.Alarm.Audio.Links.Harvestor.Utility;
using Bible.Alarm.Shared.Constants;
using System.Text.Json;

namespace Bible.Alarm.Audio.Links.Harvestor.Harvestors.Bible
{
    internal class JwBibleHarvester
    {
        #region Public API

        internal async static Task Harvest_Bible_Links(
            Dictionary<string, string> biblePublicationCodeToNameMappings,
            ConcurrentDictionary<string, string> languageCodeToNameMappings,
            ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping)
        {
            foreach (var publication in biblePublicationCodeToNameMappings)
            {
                var publicationCode = publication.Key;
                Console.WriteLine($"Starting harvest for publication: {publicationCode} ({publication.Value})");

                Dictionary<string, string> discoveredLanguages;

                // For "nwt" publication, use HTML scraping; for others, use API
                if (publicationCode.Equals("nwt", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Using HTML scraping for {publicationCode} publication.");
                    discoveredLanguages = await DiscoverLanguagesFromHtml(publicationCode);

                    if (discoveredLanguages.Count == 0)
                    {
                        Console.WriteLine($"ERROR: Could not discover any languages for publication {publicationCode} from HTML. Skipping.");
                        continue;
                    }
                }
                else
                {
                    discoveredLanguages = await DiscoverLanguagesFromApi(publicationCode);
                    if (discoveredLanguages.Count == 0)
                    {
                        continue;
                    }
                }

                // Process discovered languages
                foreach (var langEntry in discoveredLanguages)
                {
                    var languageCode = langEntry.Key;
                    var language = langEntry.Value;

                    languageCodeToNameMappings.TryAdd(languageCode, language);

                    Console.WriteLine($"Harvesting Bible chapter links for {publication.Value} of {language} language.");
                    await HarvestBibleLinks(languageCode, publicationCode);

                    if (!languageCodeToEditionsMapping.TryAdd(languageCode, new List<string>([publication.Key])))
                    {
                        languageCodeToEditionsMapping[languageCode].Add(publication.Key);
                    }
                }
            }
        }

        #endregion

        #region Language Discovery

        /// <summary>
        /// Discovers languages from the JW.org API for a given publication.
        /// </summary>
        private static async Task<Dictionary<string, string>> DiscoverLanguagesFromApi(string publicationCode)
        {
            var discoveredLanguages = new Dictionary<string, string>();
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?booknum=0&output=json&pub={publicationCode}&fileformat=MP3&alllangs=0&langwritten=E&txtCMSLang=E";

            var jsonString = await DownloadUtility.GetAsync(harvestLink);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return discoveredLanguages;
            }

            if (!root.TryGetProperty("languages", out var languages))
            {
                return discoveredLanguages;
            }

            foreach (var item in languages.EnumerateObject())
            {
                var languageCode = item.Name;

                if (!item.Value.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var language = nameElement.GetString();
                if (string.IsNullOrEmpty(language))
                {
                    continue;
                }

                discoveredLanguages[languageCode] = language;
            }

            return discoveredLanguages;
        }

        /// <summary>
        /// Scrapes the JW.org HTML page to extract language codes for a publication.
        /// Used when the API fails to return languages (e.g., for "nwt").
        /// </summary>
        private static async Task<Dictionary<string, string>> DiscoverLanguagesFromHtml(string publicationCode)
        {
            var discoveredLanguages = new Dictionary<string, string>();

            try
            {
                var htmlUrl = $"https://www.jw.org/en/library/bible/{publicationCode}/books/";
                var htmlContent = await DownloadHtmlContent(htmlUrl);

                // Extract language codes and names from <option> elements
                // Pattern matches: <option ... value="mco" ...>Mixe (North Central)</option>
                var optionPattern = @"<option[^>]*value=""([^""]+)""[^>]*>([^<]+)</option>";
                var optionMatches = Regex.Matches(htmlContent, optionPattern, RegexOptions.IgnoreCase);

                foreach (Match match in optionMatches)
                {
                    var languageCode = match.Groups[1].Value.Trim().ToUpperInvariant();
                    var languageName = match.Groups[2].Value.Trim();

                    if (string.IsNullOrWhiteSpace(languageCode) || string.IsNullOrWhiteSpace(languageName))
                        continue;

                    if (discoveredLanguages.ContainsKey(languageCode))
                        continue;

                    // Only process if this looks like a language code (1-20 chars to handle codes like "yue-hans", "cmn-hant", etc.)
                    if (languageCode.Length >= 1 && languageCode.Length <= 20)
                    {
                        discoveredLanguages[languageCode] = languageName;
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail - return empty dictionary
            }

            return discoveredLanguages;
        }

        /// <summary>
        /// Downloads HTML content from the specified URL with proper HTTP configuration.
        /// </summary>
        private static async Task<string> DownloadHtmlContent(string htmlUrl)
        {
            using var handler = new HttpClientHandler();
            handler.AllowAutoRedirect = true;
            handler.MaxAutomaticRedirections = 10;
            handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli;

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(60);

            // Try HTTP/2 first (avoids TLS renegotiation issues), fallback to HTTP/1.1 if needed
            var request = new HttpRequestMessage(HttpMethod.Get, htmlUrl)
            {
                Version = new Version(2, 0),
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };

            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; curl/8.0.1)");

            try
            {
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException)
            {
                // If HTTP/2 fails, try HTTP/1.1 as fallback
                request.Version = new Version(1, 1);
                request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

        #endregion

        #region Book and Chapter Harvesting

        /// <summary>
        /// Harvests Bible links for a specific language and publication.
        /// </summary>
        private static async Task<bool> HarvestBibleLinks(string languageCode, string publicationCode)
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

                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        doc?.Dispose();
                        continue;
                    }

                    if (!root.TryGetProperty("files", out files))
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        doc?.Dispose();
                        continue;
                    }
                }
                catch (Exception e)
                {
                    if (e is JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException)
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        doc?.Dispose();
                        continue;
                    }
                    throw;
                }

                try
                {
                    if (!files.TryGetProperty(languageCode, out var languageFiles))
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        continue;
                    }

                    if (!languageFiles.TryGetProperty("MP3", out var bookFiles))
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        continue;
                    }

                    ProcessBookFiles(bookFiles, doc.RootElement, harvestLink, bookNumberBookMap, bookNumberChapterMap, ref bookNumber);
                }
                catch (KeyNotFoundException)
                {
                    AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
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
                SaveBooksAndChapters(booksDirectory, booksIndex, bookNumberBookMap, bookNumberChapterMap);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Processes book files from the API response and populates the book and chapter maps.
        /// </summary>
        private static void ProcessBookFiles(
            JsonElement bookFiles,
            JsonElement rootElement,
            string harvestLink,
            Dictionary<int, BibleBook> bookNumberBookMap,
            Dictionary<int, Dictionary<int, BibleChapter>> bookNumberChapterMap,
            ref int bookNumber)
        {
            foreach (var bookFile in bookFiles.EnumerateArray())
            {
                if (!bookFile.TryGetProperty("file", out var fileElement) ||
                    !fileElement.TryGetProperty("url", out var urlElement))
                {
                    continue;
                }

                string url = urlElement.GetString();
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                if (!bookFile.TryGetProperty("track", out var trackElement))
                {
                    continue;
                }

                var track = trackElement.GetInt32();
                if (track == 0 || url.EndsWith(".zip"))
                {
                    continue;
                }

                if (!bookFile.TryGetProperty("booknum", out var bookNumElement))
                {
                    continue;
                }

                bookNumber = bookNumElement.GetInt32();

                if (!bookNumberBookMap.ContainsKey(bookNumber))
                {
                    var bookName = GetBookName(bookFile, rootElement, harvestLink);
                    if (string.IsNullOrEmpty(bookName))
                    {
                        continue;
                    }

                    bookNumberBookMap[bookNumber] = new BibleBook
                    {
                        Number = bookNumber,
                        Name = bookName
                    };
                }

                var trackNumber = track;
                if (!bookNumberChapterMap.ContainsKey(bookNumber))
                {
                    bookNumberChapterMap[bookNumber] = new Dictionary<int, BibleChapter>();
                }

                if (!bookNumberChapterMap[bookNumber].ContainsKey(trackNumber))
                {
                    bookNumberChapterMap[bookNumber].Add(trackNumber, new BibleChapter
                    {
                        Number = trackNumber,
                        Url = url,
                    });
                }
            }
        }

        /// <summary>
        /// Gets the book name from either the book file or root element.
        /// </summary>
        private static string GetBookName(JsonElement bookFile, JsonElement rootElement, string harvestLink)
        {
            string name;

            if (harvestLink.Contains("booknum="))
            {
                if (!rootElement.TryGetProperty("pubName", out var pubNameElement))
                {
                    return null;
                }
                name = pubNameElement.GetString()!;
            }
            else
            {
                if (!bookFile.TryGetProperty("title", out var titleElement))
                {
                    return null;
                }
                name = titleElement.GetString()!.Split('-')[0].Trim();
            }

            return name == "Psalm 1" ? "Psalms" : name;
        }

        /// <summary>
        /// Advances to the next book number and updates the harvest link.
        /// </summary>
        private static void AdvanceToNextBook(ref int bookNumber, ref string harvestLink, string publicationCode, string languageCode)
        {
            bookNumber++;
            harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
        }

        /// <summary>
        /// Saves the books and chapters to disk.
        /// </summary>
        private static void SaveBooksAndChapters(
            string booksDirectory,
            string booksIndex,
            Dictionary<int, BibleBook> bookNumberBookMap,
            Dictionary<int, Dictionary<int, BibleChapter>> bookNumberChapterMap)
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
        }

        #endregion
    }
}
