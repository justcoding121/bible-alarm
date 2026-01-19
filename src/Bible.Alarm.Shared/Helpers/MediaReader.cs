using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Helpers;

public class MediaReader(string indexRoot)
{
    public async Task<Dictionary<string, Language>> GetBiblePublicationLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, "Audio", "Bible", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetBiblePublications(string languageCode)
    {
        var root = indexRoot;
        var biblePublicationIndex = Path.Combine(root, "Audio", "Bible", languageCode, "publications.json");
        var biblePublications = await File.ReadAllTextAsync(biblePublicationIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(biblePublications)!
            .ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode)
    {
        var root = indexRoot;
        var sectionsIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, "sections.json");
        var biblePublicationSections = await File.ReadAllTextAsync(sectionsIndex);
        var sections = JsonSerializer.Deserialize<IEnumerable<BiblePublicationSection>>(biblePublicationSections)!;
        // Use BookNum if available (from UrlParams), otherwise try to parse SectionCode
        return new SortedDictionary<int, BiblePublicationSection>(sections
            .Where(s => s.BookNum.HasValue || int.TryParse(s.SectionCode, out _))
            .ToDictionary(x => x.BookNum ?? (int.TryParse(x.SectionCode, out var num) ? num : 0), x => x));
    }

    public async Task<SortedDictionary<int, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode,
        int sectionNumber)
    {
        var root = indexRoot;
        var sectionsIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, sectionNumber.ToString(),
            "tracks.json");
        var biblePublicationTracks = await File.ReadAllTextAsync(sectionsIndex);
        return new SortedDictionary<int, BiblePublicationTrack>(JsonSerializer
            .Deserialize<IEnumerable<BiblePublicationTrack>>(biblePublicationTracks)!
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
