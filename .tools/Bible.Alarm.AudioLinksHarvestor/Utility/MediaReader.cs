using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications;
using Bible.Alarm.AudioLinksHarvestor.Models.Drama;
using Bible.Alarm.AudioLinksHarvestor.Models.Music;
using Bible.Alarm.AudioLinksHarvestor.Models.Video;

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

public class MediaReader(string indexRoot)
{
    public async Task<Dictionary<string, Language>> GetBibleLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, "Audio", "Bible", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetBiblePublications(string languageCode)
    {
        var root = indexRoot;
        // Normalize language code for file path lookup (cross-platform safety)
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var bibleIndex = Path.Combine(root, "Audio", "Bible", normalizedLanguageCode, "publications.json");
        var biblePublications = await File.ReadAllTextAsync(bibleIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(biblePublications)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode)
    {
        var root = indexRoot;
        // Normalize language code and publication code for file path lookup (cross-platform safety)
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = versionCode.ToUpperInvariant();
        var sectionsIndex = Path.Combine(root, "Audio", "Bible", normalizedLanguageCode, normalizedPublicationCode, "sections.json");
        var biblePublicationSections = await File.ReadAllTextAsync(sectionsIndex);
        return new SortedDictionary<int, BiblePublicationSection>(JsonSerializer.Deserialize<IEnumerable<BiblePublicationSection>>(biblePublicationSections)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    public async Task<SortedDictionary<int, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, int sectionNumber)
    {
        var root = indexRoot;
        // Normalize language code and publication code for file path lookup (cross-platform safety)
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = versionCode.ToUpperInvariant();
        var sectionsIndex = Path.Combine(root, "Audio", "Bible", normalizedLanguageCode, normalizedPublicationCode, sectionNumber.ToString(), "tracks.json");
        var biblePublicationTracks = await File.ReadAllTextAsync(sectionsIndex);
        return new SortedDictionary<int, BiblePublicationTrack>(JsonSerializer.Deserialize<IEnumerable<BiblePublicationTrack>>(biblePublicationTracks)!
                                                   .ToDictionary(x => x.Number, x => x));
    }

    public async Task<Dictionary<string, Publication>> GetMelodyMusicReleases()
    {
        var root = indexRoot;
        // Unified structure: Audio/Music/Melodies/publications.json
        var releaseIndex = Path.Combine(root, "Audio", "Music", "Melodies", "publications.json");
        var fileContent = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(fileContent)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Audio/Music/Melodies/{publicationCode}/tracks.json
        // Normalize publication code for path consistency
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var trackIndex = Path.Combine(root, "Audio", "Music", "Melodies", normalizedPublicationCode, "tracks.json");
        var fileContent = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, MusicTrack>(JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(fileContent)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
    {
        var root = indexRoot;
        // Unified structure: Audio/Music/Vocals/languages.json
        var languageIndex = Path.Combine(root, "Audio", "Music", "Vocals", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetVocalMusicReleases(string languageCode)
    {
        var root = indexRoot;
        // Unified structure: Audio/Music/Vocals/{languageCode}/publications.json
        // Normalize language code for path consistency
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var releaseIndex = Path.Combine(root, "Audio", "Music", "Vocals", normalizedLanguageCode, "publications.json");
        var vocalReleases = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(vocalReleases)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Audio/Music/Vocals/{languageCode}/{publicationCode}/tracks.json
        // Normalize language and publication codes for path consistency
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var trackIndex = Path.Combine(root, "Audio", "Music", "Vocals", normalizedLanguageCode, normalizedPublicationCode, "tracks.json");
        var melodyTracks = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, MusicTrack>(JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(melodyTracks)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    #region Drama

    public async Task<Dictionary<string, Language>> GetDramaLanguages()
    {
        var root = indexRoot;
        // Unified structure: Dramas/languages.json (no Audio/Video prefix)
        var languageIndex = Path.Combine(root, "Dramas", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!
            .ToDictionary(x => x.Code.ToUpperInvariant(), x => x); // Normalize keys to uppercase
    }

    public async Task<Dictionary<string, Publication>> GetDramaPublications(string languageCode)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/publications.json (no Audio/Video prefix)
        // Normalize language code for file path lookup
        var normalizedCode = languageCode.ToUpperInvariant();
        var publicationsIndex = Path.Combine(root, "Dramas", normalizedCode, "publications.json");
        var publications = await File.ReadAllTextAsync(publicationsIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(publications)!
            .ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, DramaTrack>> GetDramaTracks(string languageCode, string categoryKey)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/{categoryKey}/tracks.json (no Audio/Video prefix)
        // Normalize language code and category key for file path lookup
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedCategoryKey = categoryKey.ToUpperInvariant();
        var trackIndex = Path.Combine(root, "Dramas", normalizedLanguageCode, normalizedCategoryKey, "tracks.json");
        var dramaTracks = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, DramaTrack>(JsonSerializer.Deserialize<IEnumerable<DramaTrack>>(dramaTracks)!
            .ToDictionary(x => x.Number, x => x));
    }

    #endregion

    #region Video

    public async Task<Dictionary<string, Language>> GetVideoLanguages()
    {
        var root = indexRoot;
        // Unified structure: Dramas/languages.json (no Audio/Video prefix)
        // Videos are also stored under Dramas category, IsVideo flag determines media type
        var languageIndex = Path.Combine(root, "Dramas", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!
            .ToDictionary(x => x.Code.ToUpperInvariant(), x => x); // Normalize keys to uppercase
    }

    public async Task<Dictionary<string, Publication>> GetVideoPublications(string languageCode)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/publications.json (no Audio/Video prefix)
        // Videos are also stored under Dramas category, IsVideo flag determines media type
        // Normalize language code for file path lookup
        var normalizedCode = languageCode.ToUpperInvariant();
        var publicationsIndex = Path.Combine(root, "Dramas", normalizedCode, "publications.json");
        var publications = await File.ReadAllTextAsync(publicationsIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(publications)!
            .ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, VideoEpisode>> GetVideoEpisodes(string languageCode, string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/{publicationCode}/episodes.json (no Audio/Video prefix)
        // Videos are also stored under Dramas category, IsVideo flag determines media type
        // Normalize language code and publication code for file path lookup
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var episodeIndex = Path.Combine(root, "Dramas", normalizedLanguageCode, normalizedPublicationCode, "episodes.json");
        var videoEpisodes = await File.ReadAllTextAsync(episodeIndex);
        return new SortedDictionary<int, VideoEpisode>(JsonSerializer.Deserialize<IEnumerable<VideoEpisode>>(videoEpisodes)!
            .ToDictionary(x => x.Number, x => x));
    }

    #endregion

}

