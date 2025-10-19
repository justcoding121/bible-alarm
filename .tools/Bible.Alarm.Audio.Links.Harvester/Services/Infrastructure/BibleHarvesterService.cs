using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Shared.Models.Bible;
using Bible.Alarm.Shared.Utilities;
using Newtonsoft.Json;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class BibleHarvesterService : IBibleHarvesterService
{
    public async Task HarvestBibleLinksAsync(
        Dictionary<string, string> publicationCodeToNameMappings,
        ConcurrentDictionary<string, string> languageCodeToNameMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping)
    {
        foreach (var publication in publicationCodeToNameMappings)
        {
            var publicationCode = publication.Key;
            var harvestLink = $"{UrlHelper.JwOrgIndexServiceBaseUrl}?booknum=1&output=json&pub={publicationCode}&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";

            var jsonString = await DownloadUtility.GetAsync(harvestLink);
            var model = JsonConvert.DeserializeObject<dynamic>(jsonString);

            foreach (var item in model["languages"])
            {
                var languageCode = item.Name;
                var language = model["languages"][item.Name]["name"].Value;

                languageCodeToNameMappings.TryAdd(languageCode, language);

                Console.WriteLine($"Harvesting Bible chapter links for {publication.Value} of {language} language.");
                await HarvestBibleLinks(languageCode, publicationCode);

                if (!languageCodeToEditionsMapping.TryAdd(languageCode, new List<string>(new[] { publication.Key })))
                    languageCodeToEditionsMapping[languageCode].Add(publication.Key);
            }
        }
    }

    private async Task<bool> HarvestBibleLinks(string languageCode, string publicationCode)
    {
        var booksDirectory = $"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageCode}/{publicationCode}";
        var booksIndex = $"{booksDirectory}/books.json";

        var bookNumberBookMap = new Dictionary<int, BibleBook>();
        var bookNumberChapterMap = new Dictionary<int, Dictionary<int, BibleChapter>>();

        var bookNumber = 1;
        var harvestLink = $"{UrlHelper.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";

        while (bookNumber <= 66)
        {
            var jsonString = await DownloadUtility.GetAsync(harvestLink);

            dynamic model = null;
            dynamic files = null;

            try
            {
                model = JsonConvert.DeserializeObject<dynamic>(jsonString);
                files = model["files"];
            }
            catch (Exception e)
            {
                if (e is JsonReaderException || e is ArgumentException)
                {
                    bookNumber++;
                    harvestLink = $"{UrlHelper.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
                    continue;
                }
            }

            var bookFiles = files[languageCode]["MP3"];
            foreach (var bookFile in bookFiles)
            {
                string url = bookFile["file"]["url"].Value;

                if (bookFile["track"].Value == 0 || url.EndsWith(".zip")) continue;

                bookNumber = (int)bookFile["booknum"].Value;

                if (!bookNumberBookMap.ContainsKey(bookNumber))
                {
                    var name = harvestLink.Contains("booknum=")
                        ? model["pubName"].Value
                        : bookFile["title"].Value.ToString().Split('-')[0].Trim();
                    name = name == "Psalm 1" ? "Psalms" : name;
                    bookNumberBookMap[bookNumber] = new BibleBook
                    {
                        Number = (int)bookFile["booknum"].Value,
                        Name = name
                    };
                }

                var trackNumber = (int)bookFile["track"].Value;
                if (!bookNumberChapterMap.ContainsKey(bookNumber))
                    bookNumberChapterMap[bookNumber] = [];

                if (!bookNumberChapterMap[bookNumber].ContainsKey(trackNumber))
                    bookNumberChapterMap[bookNumber].Add(trackNumber,
                        new BibleChapter
                        {
                            Number = trackNumber,
                            Url = bookFile["file"]["url"].Value
                        });
            }

            if (harvestLink.Contains("booknum=")) bookNumber++;

            harvestLink = $"{UrlHelper.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
        }

        if (bookNumberBookMap.Count <= 0) return false;

        if (!Directory.Exists(booksDirectory)) Directory.CreateDirectory(booksDirectory);

        await File.WriteAllTextAsync(booksIndex, JsonConvert.SerializeObject(bookNumberBookMap.Select(x =>
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
            File.WriteAllText(chapterIndex, JsonConvert.SerializeObject(
                bookNumberChapterMap[book.Key]
                    .Select(x => x.Value)
                    .OrderBy(x => x.Number)
                    .ToList()));
        }

        return true;
    }
}
