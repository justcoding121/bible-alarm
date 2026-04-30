using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Models.BiblePublications;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Cataloger.Utility;

public class MediaReader(string indexRoot)
{
    public async Task<Dictionary<string, Language>> GetBibleLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryBible, AppConstants.ApiEndpoints.MediaIndexLanguagesFileName);
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, Publication>> GetBiblePublications(string languageCode)
    {
        var root = indexRoot;
        // Normalize language code for file path lookup (cross-platform safety)
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var bibleIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryBible, normalizedLanguageCode, AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);
        var biblePublications = await File.ReadAllTextAsync(bibleIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(biblePublications)!.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode)
    {
        var root = indexRoot;
        // Normalize language code and publication code for file path lookup (cross-platform safety)
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = versionCode.ToUpperInvariant();
        var sectionsIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryBible, normalizedLanguageCode, normalizedPublicationCode, AppConstants.ApiEndpoints.MediaIndexSectionsFileName);
        var biblePublicationSections = await File.ReadAllTextAsync(sectionsIndex);
        return new SortedDictionary<int, BiblePublicationSection>(JsonSerializer.Deserialize<IEnumerable<BiblePublicationSection>>(biblePublicationSections)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    public async Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string sectionCode)
    {
        var root = indexRoot;
        // Normalize language code and publication code for file path lookup (cross-platform safety)
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = versionCode.ToUpperInvariant();
        var sectionsIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryBible, normalizedLanguageCode, normalizedPublicationCode, sectionCode, AppConstants.ApiEndpoints.MediaIndexTracksFileName);
        var biblePublicationTracks = await File.ReadAllTextAsync(sectionsIndex);
        var bibleTracksDict = JsonSerializer.Deserialize<IEnumerable<BiblePublicationTrack>>(biblePublicationTracks)!
            .ToDictionary(x => x.TrackCode, x => x, StringComparer.Ordinal);
        return new SortedDictionary<string, BiblePublicationTrack>(bibleTracksDict, TrackCodeComparer.Comparer);
    }

    public async Task<Dictionary<string, Publication>> GetMelodyMusicReleases()
    {
        var root = indexRoot;
        // Unified structure: Music/Melodies/publications.json
        var releaseIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderMelodies, AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);
        var fileContent = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(fileContent)!.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Music/Melodies/{publicationCode}/tracks.json
        // Normalize publication code for path consistency
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var trackIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderMelodies, normalizedPublicationCode, AppConstants.ApiEndpoints.MediaIndexTracksFileName);
        var fileContent = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, MusicTrack>(JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(fileContent)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    public async Task<Dictionary<string, (string Name, SortedDictionary<int, MusicTrack> Tracks)>> GetMelodyMusicTracksByDisc(string publicationCode)
    {
        var root = indexRoot;
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var baseDir = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderMelodies, normalizedPublicationCode);
        
        var discTracksMap = new Dictionary<string, (string Name, SortedDictionary<int, MusicTrack> Tracks)>(StringComparer.OrdinalIgnoreCase);
        
        if (!Directory.Exists(baseDir))
        {
            return discTracksMap;
        }

        // Look for disc directories (e.g., iam-1, iam-2, etc.)
        var discDirs = Directory.GetDirectories(baseDir)
            .Where(d => Path.GetFileName(d).StartsWith(publicationCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => Path.GetFileName(d));

        foreach (var discDir in discDirs)
        {
            var discCode = Path.GetFileName(discDir);
            var trackFile = Path.Combine(discDir, AppConstants.ApiEndpoints.MediaIndexTracksFileName);
            var discInfoFile = Path.Combine(discDir, AppConstants.ApiEndpoints.MediaIndexMelodyDiscInfoFileName);
            
            if (File.Exists(trackFile))
            {
                var fileContent = await File.ReadAllTextAsync(trackFile);
                var tracks = JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(fileContent)!;
                var tracksDict = new SortedDictionary<int, MusicTrack>(
                    tracks.ToDictionary(x => x.Number, x => x));
                
                // Try to get disc name from disc.json, otherwise use disc code
                string discName = discCode;
                if (File.Exists(discInfoFile))
                {
                    try
                    {
                        var discInfoContent = await File.ReadAllTextAsync(discInfoFile);
                        var discInfo = JsonSerializer.Deserialize<JsonElement>(discInfoContent);
                        if (discInfo.TryGetProperty(AppConstants.Media.PubMediaJson.NamePascal, out var nameElement))
                        {
                            discName = nameElement.GetString() ?? discCode;
                        }
                    }
                    catch (Exception)
                    {
                        // If disc.json parsing fails, use disc code as name
                    }
                }
                
                discTracksMap[discCode] = (discName, tracksDict);
            }
        }

        return discTracksMap;
    }

    public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
    {
        var root = indexRoot;
        // Unified structure: Music/Vocals/languages.json
        var languageIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderVocals, AppConstants.ApiEndpoints.MediaIndexLanguagesFileName);
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, Publication>> GetVocalMusicReleases(string languageCode)
    {
        var root = indexRoot;
        // Unified structure: Music/Vocals/{languageCode}/publications.json
        // Normalize language code for path consistency
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var releaseIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderVocals, normalizedLanguageCode, AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);
        var vocalReleases = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(vocalReleases)!.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Music/Vocals/{languageCode}/{publicationCode}/tracks.json
        // Normalize language and publication codes for path consistency
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var trackIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryMusic, AppConstants.ApiEndpoints.MediaIndexFolderVocals, normalizedLanguageCode, normalizedPublicationCode, AppConstants.ApiEndpoints.MediaIndexTracksFileName);
        var melodyTracks = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, MusicTrack>(JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(melodyTracks)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    public async Task<Dictionary<string, Language>> GetMediatorLanguages()
    {
        var root = indexRoot;
        // Unified structure: Dramas/languages.json (no Audio/Video prefix)
        var languageIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryDramas, AppConstants.ApiEndpoints.MediaIndexLanguagesFileName);
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!
            .ToDictionary(x => x.Code.ToUpperInvariant(), x => x, StringComparer.OrdinalIgnoreCase); // Normalize keys to uppercase
    }

    public async Task<Dictionary<string, Publication>> GetMediatorPublications(string languageCode)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/publications.json (no Audio/Video prefix)
        // Normalize language code for file path lookup
        var normalizedCode = languageCode.ToUpperInvariant();
        var publicationsIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryDramas, normalizedCode, AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);
        var publications = await File.ReadAllTextAsync(publicationsIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(publications)!
            .ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<SortedDictionary<string, MediatorTrack>> GetMediatorTracks(string languageCode, string categoryKey)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/{categoryKey}/tracks.json (no Audio/Video prefix)
        // Normalize language code and category key for file path lookup
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedCategoryKey = categoryKey.ToUpperInvariant();
        var trackIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryDramas, normalizedLanguageCode, normalizedCategoryKey, AppConstants.ApiEndpoints.MediaIndexTracksFileName);
        var dramaTracks = await File.ReadAllTextAsync(trackIndex);
        var mediatorTracksFlat = JsonSerializer.Deserialize<IEnumerable<MediatorTrack>>(dramaTracks)!
            .ToDictionary(x => x.TrackCode, x => x, StringComparer.Ordinal);
        return new SortedDictionary<string, MediatorTrack>(mediatorTracksFlat, TrackCodeComparer.Comparer);
    }

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetMediatorPublicationSections(string languageCode, string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/{publicationCode}/sections.json
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var sectionsIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryDramas, normalizedLanguageCode, normalizedPublicationCode, AppConstants.ApiEndpoints.MediaIndexSectionsFileName);
        var sectionsJson = await File.ReadAllTextAsync(sectionsIndex);
        var sections = JsonSerializer.Deserialize<IEnumerable<BiblePublicationSection>>(sectionsJson)!;
        return new SortedDictionary<int, BiblePublicationSection>(sections.ToDictionary(x => x.Number, x => x));
    }

    public async Task<SortedDictionary<string, MediatorTrack>> GetMediatorPublicationTracks(string languageCode, string publicationCode, string sectionCode)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/{publicationCode}/{sectionCode}/tracks.json
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var normalizedSectionCode = sectionCode.ToUpperInvariant();
        var trackIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryDramas, normalizedLanguageCode, normalizedPublicationCode, normalizedSectionCode, AppConstants.ApiEndpoints.MediaIndexTracksFileName);
        var dramaTracks = await File.ReadAllTextAsync(trackIndex);
        var mediatorSectionTracks = JsonSerializer.Deserialize<IEnumerable<MediatorTrack>>(dramaTracks)!
            .ToDictionary(x => x.TrackCode, x => x, StringComparer.Ordinal);
        return new SortedDictionary<string, MediatorTrack>(mediatorSectionTracks, TrackCodeComparer.Comparer);
    }

    public Task<Dictionary<string, Language>> GetVideoLanguages()
        => GetMediatorLanguages();

    public Task<Dictionary<string, Publication>> GetVideoPublications(string languageCode)
        => GetMediatorPublications(languageCode);

    public async Task<SortedDictionary<int, VideoEpisode>> GetVideoEpisodes(string languageCode, string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/{publicationCode}/episodes.json (no Audio/Video prefix)
        // Videos are also stored under Dramas category, IsVideo flag determines media type
        // Normalize language code and publication code for file path lookup
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var episodeIndex = Path.Combine(root, AppConstants.Media.BiblePublicationCategoryDramas, normalizedLanguageCode, normalizedPublicationCode, AppConstants.ApiEndpoints.MediaIndexVideoEpisodesFileName);
        var videoEpisodes = await File.ReadAllTextAsync(episodeIndex);
        return new SortedDictionary<int, VideoEpisode>(JsonSerializer.Deserialize<IEnumerable<VideoEpisode>>(videoEpisodes)!
            .ToDictionary(x => x.Number, x => x));
    }

}

