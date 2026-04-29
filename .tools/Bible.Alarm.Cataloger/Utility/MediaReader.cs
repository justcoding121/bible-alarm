using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Models.BiblePublications;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Cataloger.Utility;

public class MediaReader(string indexRoot)
{
    public async Task<Dictionary<string, Language>> GetBibleLanguages()
    {
        var root = indexRoot;
        var languageIndex = Path.Combine(root, "Bible", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetBiblePublications(string languageCode)
    {
        var root = indexRoot;
        // Normalize language code for file path lookup (cross-platform safety)
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var bibleIndex = Path.Combine(root, "Bible", normalizedLanguageCode, "publications.json");
        var biblePublications = await File.ReadAllTextAsync(bibleIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(biblePublications)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode)
    {
        var root = indexRoot;
        // Normalize language code and publication code for file path lookup (cross-platform safety)
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = versionCode.ToUpperInvariant();
        var sectionsIndex = Path.Combine(root, "Bible", normalizedLanguageCode, normalizedPublicationCode, "sections.json");
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
        var sectionsIndex = Path.Combine(root, "Bible", normalizedLanguageCode, normalizedPublicationCode, sectionCode, "tracks.json");
        var biblePublicationTracks = await File.ReadAllTextAsync(sectionsIndex);
        return new SortedDictionary<string, BiblePublicationTrack>(JsonSerializer.Deserialize<IEnumerable<BiblePublicationTrack>>(biblePublicationTracks)!
                                                   .ToDictionary(x => x.TrackCode, x => x));
    }

    public async Task<Dictionary<string, Publication>> GetMelodyMusicReleases()
    {
        var root = indexRoot;
        // Unified structure: Music/Melodies/publications.json
        var releaseIndex = Path.Combine(root, "Music", "Melodies", "publications.json");
        var fileContent = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(fileContent)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Music/Melodies/{publicationCode}/tracks.json
        // Normalize publication code for path consistency
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var trackIndex = Path.Combine(root, "Music", "Melodies", normalizedPublicationCode, "tracks.json");
        var fileContent = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, MusicTrack>(JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(fileContent)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    public async Task<Dictionary<string, (string Name, SortedDictionary<int, MusicTrack> Tracks)>> GetMelodyMusicTracksByDisc(string publicationCode)
    {
        var root = indexRoot;
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var baseDir = Path.Combine(root, "Music", "Melodies", normalizedPublicationCode);
        
        var discTracksMap = new Dictionary<string, (string Name, SortedDictionary<int, MusicTrack> Tracks)>();
        
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
            var trackFile = Path.Combine(discDir, "tracks.json");
            var discInfoFile = Path.Combine(discDir, "disc.json");
            
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
                    catch
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
        var languageIndex = Path.Combine(root, "Music", "Vocals", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<Dictionary<string, Publication>> GetVocalMusicReleases(string languageCode)
    {
        var root = indexRoot;
        // Unified structure: Music/Vocals/{languageCode}/publications.json
        // Normalize language code for path consistency
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var releaseIndex = Path.Combine(root, "Music", "Vocals", normalizedLanguageCode, "publications.json");
        var vocalReleases = await File.ReadAllTextAsync(releaseIndex);
        return JsonSerializer.Deserialize<IEnumerable<Publication>>(vocalReleases)!.ToDictionary(x => x.Code, x => x);
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Music/Vocals/{languageCode}/{publicationCode}/tracks.json
        // Normalize language and publication codes for path consistency
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var trackIndex = Path.Combine(root, "Music", "Vocals", normalizedLanguageCode, normalizedPublicationCode, "tracks.json");
        var melodyTracks = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<int, MusicTrack>(JsonSerializer.Deserialize<IEnumerable<MusicTrack>>(melodyTracks)!
                                                .ToDictionary(x => x.Number, x => x));
    }

    public async Task<Dictionary<string, Language>> GetMediatorLanguages()
    {
        var root = indexRoot;
        // Unified structure: Dramas/languages.json (no Audio/Video prefix)
        var languageIndex = Path.Combine(root, "Dramas", "languages.json");
        var languages = await File.ReadAllTextAsync(languageIndex);
        return JsonSerializer.Deserialize<IEnumerable<Language>>(languages)!
            .ToDictionary(x => x.Code.ToUpperInvariant(), x => x); // Normalize keys to uppercase
    }

    public async Task<Dictionary<string, Publication>> GetMediatorPublications(string languageCode)
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

    public async Task<SortedDictionary<string, MediatorTrack>> GetMediatorTracks(string languageCode, string categoryKey)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/{categoryKey}/tracks.json (no Audio/Video prefix)
        // Normalize language code and category key for file path lookup
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedCategoryKey = categoryKey.ToUpperInvariant();
        var trackIndex = Path.Combine(root, "Dramas", normalizedLanguageCode, normalizedCategoryKey, "tracks.json");
        var dramaTracks = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<string, MediatorTrack>(JsonSerializer.Deserialize<IEnumerable<MediatorTrack>>(dramaTracks)!
            .ToDictionary(x => x.TrackCode, x => x));
    }

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetMediatorPublicationSections(string languageCode, string publicationCode)
    {
        var root = indexRoot;
        // Unified structure: Dramas/{languageCode}/{publicationCode}/sections.json
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var sectionsIndex = Path.Combine(root, "Dramas", normalizedLanguageCode, normalizedPublicationCode, "sections.json");
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
        var trackIndex = Path.Combine(root, "Dramas", normalizedLanguageCode, normalizedPublicationCode, normalizedSectionCode, "tracks.json");
        var dramaTracks = await File.ReadAllTextAsync(trackIndex);
        return new SortedDictionary<string, MediatorTrack>(JsonSerializer.Deserialize<IEnumerable<MediatorTrack>>(dramaTracks)!
            .ToDictionary(x => x.TrackCode, x => x));
    }

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

}

