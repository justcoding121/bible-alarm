using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Models.Bible;
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
        var bibleIndex = Path.Combine(root, "Audio", "Bible", languageCode, "publications.json");
        var biblePublications = await File.ReadAllTextAsync(bibleIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(biblePublications)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, BibleSection>> GetBibleSections(string languageCode, string versionCode)
    {
        var root = indexRoot;
        var sectionsIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, "sections.json");
        var bibleSections = await File.ReadAllTextAsync(sectionsIndex);
        return new SortedDictionary<int, BibleSection>(JsonSerializer.Deserialize<IEnumerable<BibleSection>>(bibleSections)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    public async Task<SortedDictionary<int, BibleTrack>> GetBibleTracks(string languageCode, string versionCode, int sectionNumber)
    {
        var root = indexRoot;
        var sectionsIndex = Path.Combine(root, "Audio", "Bible", languageCode, versionCode, sectionNumber.ToString(), "tracks.json");
        var bibleTracks = await File.ReadAllTextAsync(sectionsIndex);
        return new SortedDictionary<int, BibleTrack>(JsonSerializer.Deserialize<IEnumerable<BibleTrack>>(bibleTracks)!
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
        return new SortedDictionary<int, MusicTrack>(JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(fileContent)!
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

    public async Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode)
    {
        var root = indexRoot;
        var trackIndex = Path.Combine(root, "Music", "Vocals", languageCode, publicationCode, "tracks.json");
        var melodyTracks = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, MusicTrack>(JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(melodyTracks)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    #region Drama

    public async Task<Dictionary<string, Language>> GetDramaLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, "Audio", "Drama", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!
            .ToDictionary(x => x.Code.ToUpperInvariant(), x => x); // Normalize keys to uppercase
    }

    public async Task<Dictionary<string, Publication>> GetDramaPublications(string languageCode)
    {
        var root = indexRoot;
        // Normalize language code for file path lookup
        var normalizedCode = languageCode.ToUpperInvariant();
        var publicationsIndex = Path.Combine(root, "Audio", "Drama", normalizedCode, "publications.json");
        var publications = await File.ReadAllTextAsync(publicationsIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(publications)!
            .ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, DramaTrack>> GetDramaTracks(string languageCode, string categoryKey)
    {
        var root = indexRoot;
        // Normalize language code for file path lookup
        var normalizedCode = languageCode.ToUpperInvariant();
        var trackIndex = Path.Combine(root, "Audio", "Drama", normalizedCode, categoryKey, "tracks.json");
        var dramaTracks = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, DramaTrack>(JsonSerializer.Deserialize<IEnumerable<DramaTrack>>(dramaTracks)!
            .ToDictionary(x => x.Number, x => x));
    }

    #endregion

    #region Video

    public async Task<Dictionary<string, Language>> GetVideoLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, "Video", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!
            .ToDictionary(x => x.Code.ToUpperInvariant(), x => x); // Normalize keys to uppercase
    }

    public async Task<Dictionary<string, Publication>> GetVideoPublications(string languageCode)
    {
        var root = indexRoot;
        // Normalize language code for file path lookup
        var normalizedCode = languageCode.ToUpperInvariant();
        var publicationsIndex = Path.Combine(root, "Video", normalizedCode, "publications.json");
        var publications = await File.ReadAllTextAsync(publicationsIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(publications)!
            .ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, VideoEpisode>> GetVideoEpisodes(string languageCode, string publicationCode)
    {
        var root = indexRoot;
        // Normalize language code for file path lookup
        var normalizedCode = languageCode.ToUpperInvariant();
        var episodeIndex = Path.Combine(root, "Video", normalizedCode, publicationCode, "episodes.json");
        var videoEpisodes = await File.ReadAllTextAsync(episodeIndex);
        return new SortedDictionary<int, VideoEpisode>(JsonSerializer.Deserialize<IEnumerable<VideoEpisode>>(videoEpisodes)!
            .ToDictionary(x => x.Number, x => x));
    }

    #endregion

}

