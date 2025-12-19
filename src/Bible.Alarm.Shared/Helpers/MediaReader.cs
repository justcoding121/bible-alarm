using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Helpers;

public class MediaReader(string indexRoot)
{
    public async Task<Dictionary<string, Language>> GetBibleLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, "Audio", "Bible", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetBibleTranslations(string languageCode)
    {
        var root = indexRoot;
        var bibleIndex = Path.Combine(root, "Audio", "Bible", languageCode, "publications.json");
        var bibleTranslations = await File.ReadAllTextAsync(bibleIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(bibleTranslations)!
            .ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, BibleBook>> GetBibleBooks(string languageCode, string versionCode)
    {
        var root = indexRoot;
        var booksIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, "books.json");
        var bibleBooks = await File.ReadAllTextAsync(booksIndex);
        return new SortedDictionary<int, BibleBook>(JsonSerializer.Deserialize<IEnumerable<BibleBook>>(bibleBooks)!
            .ToDictionary(x => x.Number, x => x));
    }

    public async Task<SortedDictionary<int, BibleChapter>> GetBibleChapters(string languageCode, string versionCode,
        int bookNumber)
    {
        var root = indexRoot;
        var booksIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, bookNumber.ToString(),
            "chapters.json");
        var bibleChapters = await File.ReadAllTextAsync(booksIndex);
        return new SortedDictionary<int, BibleChapter>(JsonSerializer
            .Deserialize<IEnumerable<BibleChapter>>(bibleChapters)!
            .ToDictionary(x => x.Number, x => x));
    }

    public async Task<Dictionary<string, Publication>> GetMelodyMusicReleases()
    {
        var root = indexRoot;
        var releaseIndex = Path.Combine(root, "Music", "Melodies", "publications.json");
        var fileContent = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(fileContent)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode)
    {
        var root = indexRoot;
        var trackIndex = Path.Combine(root, "Music", "Melodies", publicationCode, "tracks.json");
        var fileContent = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, MusicTrack>(JsonSerializer
            .Deserialize<IEnumerable<MusicTrack>>(fileContent)!
            .ToDictionary(x => x.Number, x => x));
    }

    public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, "Music", "Vocals", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetVocalMusicReleases(string languageCode)
    {
        var root = indexRoot;
        var releaseIndex = Path.Combine(root, "Music", "Vocals", languageCode, "publications.json");
        var vocalReleases = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(vocalReleases)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode,
        string publicationCode)
    {
        var root = indexRoot;
        var trackIndex = Path.Combine(root, "Music", "Vocals", languageCode, publicationCode, "tracks.json");
        var melodyTracks = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, MusicTrack>(JsonSerializer
            .Deserialize<IEnumerable<MusicTrack>>(melodyTracks)!
            .ToDictionary(x => x.Number, x => x));
    }
}
