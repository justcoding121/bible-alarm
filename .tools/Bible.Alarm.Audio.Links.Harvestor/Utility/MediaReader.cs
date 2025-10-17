namespace Bible.Alarm.Audio.Links.Harvestor.Utility
{
    using Advanced.Algorithms.DataStructures.Foundation;
    using AudioLinkHarvester.Models;
    using AudioLinkHarvester.Models.Bible;
    using AudioLinkHarvester.Models.Music;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public class MediaReader
    {
        private readonly string indexRoot;
        public MediaReader(string indexRoot)
        {
            this.indexRoot = indexRoot;
        }

        public async Task<Dictionary<string, Language>> GetBibleLanguages()
        {
            var root = indexRoot;
            var languageIndex = Path.Combine(root, "Audio", "Bible", "languages.json");
            var languages = await File.ReadAllTextAsync(languageIndex);
            return JsonConvert.DeserializeObject<IEnumerable<Language>>(languages).ToDictionary(x => x.Code, x => x);
        }

        public async Task<Dictionary<string, Publication>> GetBibleTranslations(string languageCode)
        {
            var root = indexRoot;
            var bibleIndex = Path.Combine(root, "Audio", "Bible", languageCode, "publications.json");
            var bibleTranslations = await File.ReadAllTextAsync(bibleIndex);
            return JsonConvert.DeserializeObject<IEnumerable<Publication>>(bibleTranslations).ToDictionary(x => x.Code, x => x);
        }

        public async Task<OrderedDictionary<int, BibleBook>> GetBibleBooks(string languageCode, string versionCode)
        {
            var root = indexRoot;
            var booksIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, "books.json");
            var bibleBooks = await File.ReadAllTextAsync(booksIndex);
            return new OrderedDictionary<int, BibleBook>(JsonConvert.DeserializeObject<IEnumerable<BibleBook>>(bibleBooks)
                                                    .Select(x => new KeyValuePair<int, BibleBook>(x.Number, x)));
        }

        public async Task<OrderedDictionary<int, BibleChapter>> GetBibleChapters(string languageCode, string versionCode, int bookNumber)
        {
            var root = indexRoot;
            var booksIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, bookNumber.ToString(), "chapters.json");
            var bibleChapters = await File.ReadAllTextAsync(booksIndex);
            return new OrderedDictionary<int, BibleChapter>(JsonConvert.DeserializeObject<IEnumerable<BibleChapter>>(bibleChapters)
                                                       .Select(x => new KeyValuePair<int, BibleChapter>(x.Number, x)));
        }

        public async Task<Dictionary<string, Publication>> GetMelodyMusicReleases()
        {
            var root = indexRoot;
            var releaseIndex = Path.Combine(root, "Music", "Melodies", "publications.json");
            var fileContent = await File.ReadAllTextAsync(releaseIndex);
            return JsonConvert.DeserializeObject<IEnumerable<Publication>>(fileContent).ToDictionary(x => x.Code, x => x);
        }

        public async Task<OrderedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode)
        {
            var root = indexRoot;
            var trackIndex = Path.Combine(root, "Music", "Melodies", publicationCode, "tracks.json");
            var fileContent = await File.ReadAllTextAsync(trackIndex);
            return new OrderedDictionary<int, MusicTrack>(JsonConvert.DeserializeObject<IEnumerable<MusicTrack>>(fileContent)
                                                    .Select(x => new KeyValuePair<int, MusicTrack>(x.Number, x)));
        }

        public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
        {
            var root = indexRoot;
            var languageIndex = Path.Combine(root, "Music", "Vocals", "languages.json");
            var languages = await File.ReadAllTextAsync(languageIndex);
            return JsonConvert.DeserializeObject<IEnumerable<Language>>(languages).ToDictionary(x => x.Code, x => x);
        }

        public async Task<Dictionary<string, Publication>> GetVocalMusicReleases(string languageCode)
        {
            var root = indexRoot;
            var releaseIndex = Path.Combine(root, "Music", "Vocals", languageCode, "publications.json");
            var vocalReleases = await File.ReadAllTextAsync(releaseIndex);
            return JsonConvert.DeserializeObject<IEnumerable<Publication>>(vocalReleases).ToDictionary(x => x.Code, x => x);
        }

        public async Task<OrderedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode)
        {
            var root = indexRoot;
            var trackIndex = Path.Combine(root, "Music", "Vocals", languageCode, publicationCode, "tracks.json");
            var melodyTracks = await File.ReadAllTextAsync(trackIndex);
            return new OrderedDictionary<int, MusicTrack>(JsonConvert.DeserializeObject<IEnumerable<MusicTrack>>(melodyTracks)
                                                    .Select(x => new KeyValuePair<int, MusicTrack>(x.Number, x)));
        }

    }
}


