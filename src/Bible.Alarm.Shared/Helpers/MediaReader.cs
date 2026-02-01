using System;
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
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.LanguageCode, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetBiblePublications(string languageCode)
    {
        var root = indexRoot;
        var biblePublicationIndex = Path.Combine(root, "Audio", "Bible", languageCode, "publications.json");
        var biblePublications = await File.ReadAllTextAsync(biblePublicationIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(biblePublications)!
            .ToDictionary(x => x.PublicationCode, x => x);
    }

    public async Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode)
    {
        var root = indexRoot;
        var sectionsIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, "sections.json");
        var biblePublicationSections = await File.ReadAllTextAsync(sectionsIndex);
        var sections = JsonSerializer.Deserialize<IEnumerable<BiblePublicationSection>>(biblePublicationSections)!;
        // Keep SectionCode as string end-to-end; only parse for ordering.
        // Use a SortedDictionary with the shared natural comparer for SectionCode.
        var map = new Dictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections)
        {
            var code = SectionCodeHelper.Normalize(section.SectionCode);
            if (string.IsNullOrEmpty(code))
            {
                continue;
            }

            // Keep first occurrence if duplicates exist.
            map.TryAdd(code, section);
        }

        return new SortedDictionary<string, BiblePublicationSection>(map, SectionCodeHelper.SectionCodeComparer);
    }

    public async Task<SortedDictionary<int, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode,
        string sectionCode)
    {
        var root = indexRoot;
        var sectionsIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, sectionCode,
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
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(fileContent)!.ToDictionary(x => x.PublicationCode, x => x);
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
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.LanguageCode, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetVocalMusicReleases(string languageCode)
    {
        var root = indexRoot;
        var releaseIndex = Path.Combine(root, "Music", "Vocals", languageCode, "publications.json");
        var vocalReleases = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(vocalReleases)!.ToDictionary(x => x.PublicationCode, x => x);
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
