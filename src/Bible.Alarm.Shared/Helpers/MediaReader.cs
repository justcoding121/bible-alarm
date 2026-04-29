using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Helpers;

public class MediaReader(string indexRoot)
{
    public async Task<Dictionary<string, Language>> GetBiblePublicationLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, AppConstants.ApiEndpoints.MediaIndexFolderAudio, AppConstants.Media.BiblePublicationCategoryBible, AppConstants.ApiEndpoints.MediaIndexLanguagesFileName);
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.LanguageCode, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetBiblePublications(string languageCode)
    {
        var root = indexRoot;
        var biblePublicationIndex = Path.Combine(root, AppConstants.ApiEndpoints.MediaIndexFolderAudio, AppConstants.Media.BiblePublicationCategoryBible, languageCode, AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);
        var biblePublications = await File.ReadAllTextAsync(biblePublicationIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(biblePublications)!
            .ToDictionary(x => x.PublicationCode, x => x);
    }

    public async Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode)
    {
        var root = indexRoot;
        var sectionsIndex = Path.Combine(root, AppConstants.ApiEndpoints.MediaIndexFolderAudio, AppConstants.Media.BiblePublicationCategoryBible, languageCode, versionCode, AppConstants.ApiEndpoints.MediaIndexSectionsFileName);
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

    public async Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode,
        string sectionCode)
    {
        var root = indexRoot;
        var sectionsIndex = Path.Combine(root, AppConstants.ApiEndpoints.MediaIndexFolderAudio, AppConstants.Media.BiblePublicationCategoryBible, languageCode, versionCode, sectionCode, AppConstants.ApiEndpoints.MediaIndexTracksFileName);
        var biblePublicationTracks = await File.ReadAllTextAsync(sectionsIndex);
        // Parse TrackCode as int for dictionary key (for backward compatibility)
        var tracks = JsonSerializer.Deserialize<IEnumerable<BiblePublicationTrack>>(biblePublicationTracks)!;
        var dict = new Dictionary<string, BiblePublicationTrack>();
        foreach (var track in tracks)
        {
            dict[track.TrackCode] = track;
        }
        return new SortedDictionary<string, BiblePublicationTrack>(dict, TrackCodeComparer.Comparer);
    }

    public async Task<Dictionary<string, Publication>> GetMelodyMusicReleases()
    {
        var root = indexRoot;
        var releaseIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderMelodies, AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);
        var fileContent = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(fileContent)!.ToDictionary(x => x.PublicationCode, x => x);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode)
    {
        var root = indexRoot;
        var trackIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderMelodies, publicationCode, AppConstants.ApiEndpoints.MediaIndexTracksFileName);
        var fileContent = await File.ReadAllTextAsync(trackIndex);
        var list = JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(fileContent)!.ToList();
        list.Sort((a, b) => a.CompareTo(b));
        var dict = new Dictionary<int, MusicTrack>();
        for (var i = 0; i < list.Count; i++)
            dict[i] = list[i];
        return new SortedDictionary<int, MusicTrack>(dict);
    }

    public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderVocals, AppConstants.ApiEndpoints.MediaIndexLanguagesFileName);
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.LanguageCode, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetVocalMusicReleases(string languageCode)
    {
        var root = indexRoot;
        var releaseIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderVocals, languageCode, AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);
        var vocalReleases = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(vocalReleases)!.ToDictionary(x => x.PublicationCode, x => x);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode,
        string publicationCode)
    {
        var root = indexRoot;
        var trackIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderVocals, languageCode, publicationCode, AppConstants.ApiEndpoints.MediaIndexTracksFileName);
        var melodyTracks = await File.ReadAllTextAsync(trackIndex);
        var list = JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(melodyTracks)!.ToList();
        list.Sort((a, b) => a.CompareTo(b));
        var dict = new Dictionary<int, MusicTrack>();
        for (var i = 0; i < list.Count; i++)
            dict[i] = list[i];
        return new SortedDictionary<int, MusicTrack>(dict);
    }
}
