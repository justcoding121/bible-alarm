using AudioLinkHarvester.Models;
using AudioLinkHarvester.Models.Music;
using AudioLinkHarvester.Utility;
using AudioLinkHarvestor.Utility;
using Bible.Alarm.Common.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AudioLinkHarvester.Audio
{
    internal class MusicHarverster
    {
        private static Dictionary<string, string> vocalsPublicationCodeToNameMappings = new Dictionary<string, string>(new KeyValuePair<string, string>[]{
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
                var harvestLink = $"{UrlHelper.JwOrgIndexServiceBaseUrl}?booknum=0&output=json&pub={publication.Key}&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";

                var jsonString = await DownloadUtility.GetAsync(harvestLink);
                var model = JsonConvert.DeserializeObject<dynamic>(jsonString);

                foreach (var item in model["languages"])
                {
                    var languageCode = item.Name;
                    var language = model["languages"][item.Name]["name"].Value;

                    Console.WriteLine($"Harvesting Music track links for {publication.Value} of {language} language.");

                    try
                    {
                        await MusicHarverster.harvestMusicLinks(publication.Key, new List<string>(new[] { publication.Key }), languageCode);
                        languageCodeToNames[languageCode] = language;

                        if (languageCodeToPublications.ContainsKey(languageCode))
                        {
                            languageCodeToPublications[languageCode].Add(publication.Key);
                        }
                        else
                        {
                            languageCodeToPublications[languageCode] = new List<string>(new[] { publication.Key });
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

                File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languagePublication.Key}/publications.json", JsonConvert.SerializeObject(
                 languagePublication.Value.Select(x => new
                 Publication
                 {
                     Code = x,
                     Name = vocalsPublicationCodeToNameMappings[x]
                 }).OrderBy(x => x.Code)));
            }

            File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/languages.json",
            JsonConvert.SerializeObject(languageCodeToPublications.Select(x =>
            new Language
            {
                Code = x.Key,
                Name = languageCodeToNames[x.Key]
            }).OrderBy(x => x.Code)));

        }

        private static Dictionary<string, string> melodyPublicationCodeToNameMappings = new Dictionary<string, string>(new KeyValuePair<string, string>[]{
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
                    for (int i = 1; i <= 9; i++)
                    {
                        //we don't have these discs
                        if (i == 7 || i == 8)
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

            File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/publications.json", JsonConvert.SerializeObject(
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

            int trackNumber = 1;
            var musicTracks = new List<MusicTrack>();

            foreach (var publicationDownloadCode in publicationDownloadCodes)
            {
                var harvestLink = $"{UrlHelper.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationDownloadCode}&fileformat=MP3&alllangs=0{(languageCode == null ? $"&langwritten=E" : $"&langwritten={languageCode}")}&txtCMSLang=E";

                var jsonString = await DownloadUtility.GetAsync(harvestLink);

                var model = JsonConvert.DeserializeObject<dynamic>(jsonString);

                var lc = languageCode ?? "E";

                var musicFiles = model["files"][lc]["MP3"];
                foreach (var musicFile in musicFiles)
                {
                    string url = musicFile["file"]["url"].Value;
                    int track = (int)musicFile["track"].Value;

                    if (track == 0
                        || url.EndsWith(".zip"))
                        continue;

                    double duration = (double)musicFile["duration"];
                    musicTracks.Add(new MusicTrack()
                    {
                        Number = trackNumber,
                        Title = musicFile["title"].Value,
                        Url = url,
                        LookUpPath = $"?output=json&pub={publicationDownloadCode}&fileformat=MP3" +
                                    $"{(languageCode == null ? $"&langwritten=E" : $"&langwritten={languageCode}")}" +
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

                File.WriteAllText(file, JsonConvert.SerializeObject(musicTracks.OrderBy(x => x.Number)));
                return true;
            }

            return false;
        }

    }
}
