using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvestor.Models;
using Bible.Alarm.Audio.Links.Harvestor.Models.Music;
using Bible.Alarm.Audio.Links.Harvestor.Utility;
using Bible.Alarm.Shared.Constants;
using System.Text.Json;

namespace Bible.Alarm.Audio.Links.Harvestor.Harvestors.Music
{
    internal class MusicHarverster
    {
        private static Dictionary<string, string> vocalsPublicationCodeToNameMappings = new Dictionary<string, string>(new[]{
            new KeyValuePair<string, string>("osg","Original Songs"),
            new KeyValuePair<string, string>("sjjc","\"Sing Out Joyfully\" to Jehovah (2016)"),
            new KeyValuePair<string, string>("snv","Sing to Jehovah (2014) ")
        });

        internal async static Task Harvest_Vocal_Music_Links()
        {
            var languageCodeToNames = new Dictionary<string, string>();
            var languageCodeToPublications = new Dictionary<string, List<string>>();

            foreach (var publication in vocalsPublicationCodeToNameMappings)
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?booknum=0&output=json&pub={publication.Key}&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";

                var jsonString = await DownloadUtility.GetAsync(harvestLink);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                var languages = root.GetProperty("languages");

                foreach (var item in languages.EnumerateObject())
                {
                    var languageCode = item.Name;
                    var language = item.Value.GetProperty("name").GetString();

                    Console.WriteLine($"Harvesting Music track links for {publication.Value} of {language} language.");

                    try
                    {
                        await harvestMusicLinks(publication.Key, new List<string>([publication.Key]), languageCode);
                        languageCodeToNames[languageCode] = language;

                        if (languageCodeToPublications.ContainsKey(languageCode))
                        {
                            languageCodeToPublications[languageCode].Add(publication.Key);
                        }
                        else
                        {
                            languageCodeToPublications[languageCode] = new List<string>([publication.Key]);
                        }

                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Failed: Harvesting Music track links for {publication.Value} of {language} language. Exception: {e}");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }

            }

            foreach (var languagePublication in languageCodeToPublications)
            {
                if (!Directory.Exists($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languagePublication.Key}"))
                {
                    Directory.CreateDirectory($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languagePublication.Key}");
                }

                File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languagePublication.Key}/publications.json", JsonSerializer.Serialize(
                 languagePublication.Value.Select(x => new
                 Publication
                 {
                     Code = x,
                     Name = vocalsPublicationCodeToNameMappings[x]
                 }).OrderBy(x => x.Code)));
            }

            File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/languages.json",
            JsonSerializer.Serialize(languageCodeToPublications.Select(x =>
            new Language
            {
                Code = x.Key,
                Name = languageCodeToNames[x.Key]
            }).OrderBy(x => x.Code)));

        }

        private static Dictionary<string, string> melodyPublicationCodeToNameMappings = new Dictionary<string, string>(new[]{
            new KeyValuePair<string, string>("iam","Sing Praises to Jehovah (1984)")
        });

        internal async static Task Harvest_Music_Melody_Links()
        {
            var discs = new List<string>();
            var downloadCodes = new List<string>();

            foreach (var publication in melodyPublicationCodeToNameMappings)
            {
                downloadCodes.Clear();
                //multiple discs for 1984 melodies
                if (publication.Key == "iam")
                {
                    for (var i = 1; i <= 9; i++)
                    {
                        //we don't have these discs
                        if (i is 7 or 8)
                            continue;

                        downloadCodes.Add($"{publication.Key}-{i}");
                    }
                }
                else
                {
                    downloadCodes.Add(publication.Key);
                }

                Console.WriteLine($"Harvesting Music track links for {publication.Value}.");
                await harvestMusicLinks(publication.Key, downloadCodes);
            }

            File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/publications.json", JsonSerializer.Serialize(
            melodyPublicationCodeToNameMappings.Select(x => new
            Publication
            {
                Code = x.Key,
                Name = x.Value
            }).OrderBy(x => x.Code)));
        }

        private static async Task<bool> harvestMusicLinks(string publicationCode, List<string> publicationDownloadCodes, string languageCode = null)
        {
            var dir = languageCode == null ? $"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/{publicationCode}" :
                                             $"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languageCode}/{publicationCode}";
            var file = $"{dir}/tracks.json";

            var trackNumber = 1;
            var musicTracks = new List<MusicTrack>();

            foreach (var publicationDownloadCode in publicationDownloadCodes)
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationDownloadCode}&fileformat=MP3&alllangs=0{(languageCode == null ? "&langwritten=E" : $"&langwritten={languageCode}")}&txtCMSLang=E";

                var jsonString = await DownloadUtility.GetAsync(harvestLink);

                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                var lc = languageCode ?? "E";

                var musicFiles = root.GetProperty("files").GetProperty(lc).GetProperty("MP3");
                foreach (var musicFile in musicFiles.EnumerateArray())
                {
                    string url = musicFile.GetProperty("file").GetProperty("url").GetString()!;
                    var track = musicFile.GetProperty("track").GetInt32();

                    if (track == 0
                        || url.EndsWith(".zip"))
                        continue;

                    var duration = musicFile.GetProperty("duration").GetDouble();
                    musicTracks.Add(new MusicTrack
                    {
                        Number = trackNumber,
                        Title = musicFile.GetProperty("title").GetString()!,
                        Url = url,
                        LookUpPath = $"?output=json&pub={publicationDownloadCode}&fileformat=MP3" +
                                    $"{(languageCode == null ? "&langwritten=E" : $"&langwritten={languageCode}")}" +
                                    $"&txtCMSLang=E&track={track}"
                    });

                    trackNumber++;
                }

            }

            if (musicTracks.Count > 0)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(file, JsonSerializer.Serialize(musicTracks.OrderBy(x => x.Number)));
                return true;
            }

            return false;
        }

    }
}
