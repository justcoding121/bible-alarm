#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using BiblePublicationSection = Bible.Alarm.AudioLinksHarvestor.Models.Bible.BiblePublicationSection;
using BiblePublicationTrack = Bible.Alarm.AudioLinksHarvestor.Models.Bible.BiblePublicationTrack;
using DramaTrack = Bible.Alarm.AudioLinksHarvestor.Models.Drama.DramaTrack;
using VideoEpisode = Bible.Alarm.AudioLinksHarvestor.Models.Video.VideoEpisode;
using MusicTrack = Bible.Alarm.AudioLinksHarvestor.Models.Music.MusicTrack;
using Publication = Bible.Alarm.AudioLinksHarvestor.Models.Publication;

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

public class DbSeeder(ILogger logger, IServiceScopeFactory scopeFactory)
{
    // Cache for base URLs to avoid duplicate lookups
    private readonly Dictionary<string, AudioSourceBaseUrl> baseUrlCache = new();

    public async Task Seed()
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = DELETE;");
            await db.Database.MigrateAsync();
        }

        var indexDir = DirectoryHelper.IndexDirectory;
        var mediaDir = Path.Combine(indexDir, "media");
        await SeedBiblePublications(mediaDir);
        await SeedDramas(mediaDir);
        await SeedVideos(mediaDir);
        await SeedMelodies(mediaDir);
        await SeedVocals(mediaDir);
    }

    private async Task SeedBiblePublications(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var displayLanguage = await GetOrCreateDisplayLanguage(db);
        var mediaReader = new MediaReader(indexDir);

        var bibleLanguages = await GetBibleLanguagesSafely(mediaReader);
        if (bibleLanguages == null || bibleLanguages.Count == 0)
        {
            return;
        }

        foreach (var language in bibleLanguages)
        {
            var newLanguage = await GetOrCreateLanguage(db, language.Value.Code, language.Value.Name);
            await SeedPublicationsForLanguage(db, mediaReader, language.Key, newLanguage, displayLanguage);
        }
    }

    private async Task<Language> GetOrCreateDisplayLanguage(MediaDbContext db)
    {
        var displayLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Name == "English" && x.Code == "E");
        if (displayLanguage == null)
        {
            displayLanguage = new Language
            {
                Code = "E",
                Name = "English"
            };
            db.Languages.Add(displayLanguage);
            await db.SaveChangesAsync();
        }
        return displayLanguage;
    }

    private async Task<Language> GetOrCreateLanguage(MediaDbContext db, string code, string name)
    {
        // Normalize code to uppercase for consistent storage and comparison
        var normalizedCode = code.ToUpperInvariant();
        
        // Case-insensitive lookup by code only (name may vary between sources)
        var language = await db.Languages.FirstOrDefaultAsync(x => x.Code.ToUpper() == normalizedCode);
        if (language == null)
        {
            language = new Language
            {
                Code = normalizedCode,
                Name = name
            };
        }
        return language;
    }

    /// <summary>
    /// Gets an existing language by code (case-insensitive) or creates one with code as the name (fallback).
    /// Used for drama seeding where names come from existing Language table.
    /// </summary>
    private async Task<Language> GetOrCreateLanguageByCode(MediaDbContext db, string code)
    {
        // Normalize code to uppercase for consistent storage and comparison
        var normalizedCode = code.ToUpperInvariant();
        
        // Case-insensitive lookup by code only
        var language = await db.Languages.FirstOrDefaultAsync(x => x.Code.ToUpper() == normalizedCode);
        if (language == null)
        {
            // Create with code as name (fallback for languages only in drama, not in Bible/Music)
            language = new Language
            {
                Code = normalizedCode,
                Name = normalizedCode // Use code as name - will be overwritten if found later
            };
            db.Languages.Add(language);
            await db.SaveChangesAsync();
        }
        return language;
    }

    private static async Task<T?> GetSafely<T>(Func<Task<T>> getter) where T : class
    {
        try
        {
            return await getter();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private async Task<Dictionary<string, Models.Language>?> GetBibleLanguagesSafely(MediaReader mediaReader)
    {
        return await GetSafely(() => mediaReader.GetBibleLanguages());
    }

    private async Task SeedPublicationsForLanguage(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageKey,
        Language newLanguage,
        Language displayLanguage)
    {
        var publications = await GetSafely(() => mediaReader.GetBiblePublications(languageKey));
        if (publications == null || publications.Count == 0)
        {
            return;
        }

        // Load existing publication codes for this language upfront (one query)
        var existingCodesList = await db.BiblePublications
            .Where(t => t.Language.Code == languageKey)
            .Select(t => t.Code)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        foreach (var publication in publications)
        {
            // Skip if already exists (in-memory check, fast)
            if (existingCodes.Contains(publication.Value.Code))
            {
                logger.Information("Skipping publication {PublicationName} ({PublicationCode}) for language {LanguageCode} - already exists",
                    publication.Value.Name, publication.Value.Code, languageKey);
                continue;
            }

            logger.Information("Seeding publication {PublicationName} ({PublicationCode}) for language {LanguageCode}",
                publication.Value.Name, publication.Value.Code, languageKey);

            var sections = await GetSafely(() => mediaReader.GetBiblePublicationSections(languageKey, publication.Key));
            if (sections == null || sections.Count == 0)
            {
                continue;
            }

            var biblePublication = CreateBiblePublication(publication.Value, newLanguage, displayLanguage);
            await SeedSectionsForPublication(db, mediaReader, languageKey, publication.Key, sections, biblePublication);

            await db.BiblePublications.AddAsync(biblePublication);
            await db.SaveChangesAsync();
        }
    }

    private static BiblePublication CreateBiblePublication(Publication publication, Language newLanguage, Language displayLanguage)
    {
        return new BiblePublication
        {
            Name = publication.Name,
            Code = publication.Code,
            Language = newLanguage,
            DisplayLanguage = displayLanguage
        };
    }

    private async Task SeedSectionsForPublication(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageKey,
        string publicationKey,
        SortedDictionary<int, BiblePublicationSection> sections,
        BiblePublication biblePublication)
    {
        foreach (var section in sections)
        {
            var newSection = new Shared.Models.Media.Bible.BiblePublicationSection
            {
                Name = section.Value.Name,
                Number = section.Value.Number
            };

            biblePublication.Sections.Add(newSection);

            var tracks = await GetSafely(() => mediaReader.GetBiblePublicationTracks(languageKey, publicationKey, section.Key));
            if (tracks == null || tracks.Count == 0)
            {
                continue;
            }

            await AddTracksToSection(db, tracks, newSection);
        }
    }

    private async Task AddTracksToSection(
        MediaDbContext db,
        SortedDictionary<int, BiblePublicationTrack> tracks,
        Shared.Models.Media.Bible.BiblePublicationSection newSection)
    {
        foreach (var track in tracks)
        {
            var audioSource = await CreateAudioSource(db, track.Value.Url);
            var newTrack = new Shared.Models.Media.Bible.BiblePublicationTrack
            {
                Number = track.Value.Number,
                Source = audioSource
            };

            newSection.Tracks.Add(newTrack);
        }
    }

    /// <summary>
    /// Creates an AudioSource by extracting and caching the base URL.
    /// </summary>
    private async Task<AudioSource> CreateAudioSource(MediaDbContext db, string fullUrl)
    {
        var (baseUrl, urlPath) = ExtractBaseUrlAndPath(fullUrl);
        var baseUrlEntity = await GetOrCreateBaseUrl(db, baseUrl);
        
        return new AudioSource
        {
            BaseUrlEntity = baseUrlEntity,
            BaseUrlId = baseUrlEntity.Id,
            UrlPath = urlPath
        };
    }

    /// <summary>
    /// Extracts the base URL (scheme + host) and path from a full URL.
    /// </summary>
    private static (string BaseUrl, string UrlPath) ExtractBaseUrlAndPath(string fullUrl)
    {
        if (string.IsNullOrEmpty(fullUrl))
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            var uri = new Uri(fullUrl);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            var urlPath = uri.PathAndQuery;
            return (baseUrl, urlPath);
        }
        catch (UriFormatException)
        {
            // If URL is malformed, store the whole thing as the path
            return (string.Empty, fullUrl);
        }
    }

    /// <summary>
    /// Gets an existing base URL or creates a new one.
    /// Always checks the current database context first to ensure entities are tracked correctly.
    /// </summary>
    private async Task<AudioSourceBaseUrl> GetOrCreateBaseUrl(MediaDbContext db, string baseUrl)
    {
        // Always check database first in current context to ensure entity is tracked by this context
        // Don't use cached entities directly as they may be from a different DbContext scope
        var existing = await db.AudioSourceBaseUrls.FirstOrDefaultAsync(x => x.BaseUrl == baseUrl);
        if (existing != null)
        {
            // Update cache with entity from current context
            baseUrlCache[baseUrl] = existing;
            return existing;
        }

        // Create new
        var newBaseUrl = new AudioSourceBaseUrl { BaseUrl = baseUrl };
        db.AudioSourceBaseUrls.Add(newBaseUrl);
        await db.SaveChangesAsync();
        
        baseUrlCache[baseUrl] = newBaseUrl;
        return newBaseUrl;
    }

    #region Drama Seeding

    private async Task SeedDramas(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var displayLanguage = await GetOrCreateDisplayLanguage(db);
        var mediaReader = new MediaReader(indexDir);

        var dramaLanguages = await GetSafely(() => mediaReader.GetDramaLanguages());
        if (dramaLanguages == null || dramaLanguages.Count == 0)
        {
            logger.Information("No drama languages found to seed.");
            return;
        }

        logger.Information("Found {Count} drama languages to seed.", dramaLanguages.Count);

        foreach (var language in dramaLanguages)
        {
            var newLanguage = await GetOrCreateLanguageByCode(db, language.Value.Code);
            await SeedDramaPublicationsForLanguage(db, mediaReader, language.Key, newLanguage, displayLanguage);
        }
    }

    private async Task SeedDramaPublicationsForLanguage(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageCode,
        Language language,
        Language displayLanguage)
    {
        var dramaPublications = await GetSafely(() => mediaReader.GetDramaPublications(languageCode));
        if (dramaPublications == null || dramaPublications.Count == 0)
        {
            return;
        }

        // Load existing publication codes for this language upfront (one query)
        var existingCodesList = await db.BiblePublications
            .Where(p => p.Language.Code == languageCode)
            .Select(p => p.Code)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        foreach (var publication in dramaPublications)
        {
            // Skip if already exists
            if (existingCodes.Contains(publication.Value.Code))
            {
                logger.Information("Skipping drama publication {PublicationName} ({PublicationCode}) for language {LanguageCode} - already exists",
                    publication.Value.Name, publication.Value.Code, languageCode);
                continue;
            }

            logger.Information("Seeding drama publication {PublicationName} ({PublicationCode}) for language {LanguageCode}",
                publication.Value.Name, publication.Value.Code, languageCode);

            var tracks = await GetSafely(() => mediaReader.GetDramaTracks(languageCode, publication.Key));
            if (tracks == null || tracks.Count == 0)
            {
                continue;
            }

            var newPublication = new BiblePublication
            {
                Name = publication.Value.Name,
                Code = publication.Value.Code,
                Language = language,
                DisplayLanguage = displayLanguage
            };

            await AddTracksToPublication(db, tracks, newPublication);

            await db.BiblePublications.AddAsync(newPublication);
            await db.SaveChangesAsync();
        }
    }

    private async Task AddTracksToPublication(
        MediaDbContext db,
        SortedDictionary<int, DramaTrack> tracks,
        BiblePublication publication)
    {
        foreach (var track in tracks)
        {
            var audioSource = await CreateAudioSource(db, track.Value.Url);
            var newTrack = new Shared.Models.Media.Bible.BiblePublicationTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Source = audioSource
            };

            publication.Tracks.Add(newTrack);
        }
    }

    #endregion

    #region Video Seeding

    private async Task SeedVideos(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var displayLanguage = await GetOrCreateDisplayLanguage(db);
        var mediaReader = new MediaReader(indexDir);

        var videoLanguages = await GetSafely(() => mediaReader.GetVideoLanguages());
        if (videoLanguages == null || videoLanguages.Count == 0)
        {
            logger.Information("No video languages found to seed.");
            return;
        }

        logger.Information("Found {Count} video languages to seed.", videoLanguages.Count);

        foreach (var language in videoLanguages)
        {
            var newLanguage = await GetOrCreateLanguageByCode(db, language.Value.Code);
            await SeedVideoPublicationsForLanguage(db, mediaReader, language.Key, newLanguage, displayLanguage);
        }
    }

    private async Task SeedVideoPublicationsForLanguage(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageCode,
        Language language,
        Language displayLanguage)
    {
        var videoPublications = await GetSafely(() => mediaReader.GetVideoPublications(languageCode));
        if (videoPublications == null || videoPublications.Count == 0)
        {
            return;
        }

        // Load existing publication codes for this language upfront (one query)
        var existingCodesList = await db.BiblePublications
            .Where(p => p.Language.Code == languageCode)
            .Select(p => p.Code)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        foreach (var publication in videoPublications)
        {
            // Skip if already exists
            if (existingCodes.Contains(publication.Value.Code))
            {
                logger.Information("Skipping video publication {PublicationName} ({PublicationCode}) for language {LanguageCode} - already exists",
                    publication.Value.Name, publication.Value.Code, languageCode);
                continue;
            }

            logger.Information("Seeding video publication {PublicationName} ({PublicationCode}) for language {LanguageCode}",
                publication.Value.Name, publication.Value.Code, languageCode);

            var episodes = await GetSafely(() => mediaReader.GetVideoEpisodes(languageCode, publication.Key));
            if (episodes == null || episodes.Count == 0)
            {
                continue;
            }

            var newPublication = new BiblePublication
            {
                Name = publication.Value.Name,
                Code = publication.Value.Code,
                Language = language,
                DisplayLanguage = displayLanguage
            };

            await AddEpisodesToPublication(db, episodes, newPublication);

            await db.BiblePublications.AddAsync(newPublication);
            await db.SaveChangesAsync();
        }
    }

    private async Task AddEpisodesToPublication(
        MediaDbContext db,
        SortedDictionary<int, VideoEpisode> episodes,
        BiblePublication publication)
    {
        foreach (var episode in episodes)
        {
            var audioSource = await CreateAudioSource(db, episode.Value.Url);
            var newTrack = new Shared.Models.Media.Bible.BiblePublicationTrack
            {
                Number = episode.Value.Number,
                Title = episode.Value.Title,
                Source = audioSource
            };

            publication.Tracks.Add(newTrack);
        }
    }

    #endregion

    private async Task SeedMelodies(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var displayLanguage = await GetOrCreateDisplayLanguage(db);
        var mediaReader = new MediaReader(indexDir);

        var melodyMusicReleases = await GetSafely(() => mediaReader.GetMelodyMusicReleases());
        if (melodyMusicReleases == null || melodyMusicReleases.Count == 0)
        {
            return;
        }

        // Load existing melody codes upfront (one query)
        var existingCodesList = await db.MelodyMusic
            .Select(m => m.Code)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        foreach (var melodyMusicRelease in melodyMusicReleases)
        {
            // Skip if already exists (in-memory check, fast)
            if (existingCodes.Contains(melodyMusicRelease.Value.Code))
            {
                logger.Information("Skipping melody {MelodyCode} - already exists", melodyMusicRelease.Key);
                continue;
            }

            logger.Information("Seeding melody code {MelodyCode} music to database.", melodyMusicRelease.Key);

            var tracks = await GetSafely(() => mediaReader.GetMelodyMusicTracks(melodyMusicRelease.Key));
            if (tracks == null || tracks.Count == 0)
            {
                continue;
            }

            var newMelodyMusic = CreateMelodyMusic(melodyMusicRelease.Value, displayLanguage);
            await AddTracksToMelodyMusic(db, tracks, newMelodyMusic);

            await db.MelodyMusic.AddAsync(newMelodyMusic);
            await db.SaveChangesAsync();
        }
    }

    private static MelodyMusic CreateMelodyMusic(Publication melodyMusicRelease, Language displayLanguage)
    {
        return new MelodyMusic
        {
            Code = melodyMusicRelease.Code,
            Name = melodyMusicRelease.Name,
            DisplayLanguage = displayLanguage
        };
    }

    private async Task AddTracksToMelodyMusic(MediaDbContext db, SortedDictionary<int, MusicTrack> tracks, MelodyMusic newMelodyMusic)
    {
        foreach (var track in tracks)
        {
            var audioSource = await CreateAudioSource(db, track.Value.Url);
            var newTrack = new Shared.Models.Media.Music.MusicTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Source = audioSource
            };

            newMelodyMusic.Tracks.Add(newTrack);
        }
    }

    private async Task SeedVocals(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var displayLanguage = await GetOrCreateDisplayLanguage(db);
        var mediaReader = new MediaReader(indexDir);

        var melodyLanguages = await GetSafely(() => mediaReader.GetVocalMusicLanguages());
        if (melodyLanguages == null || melodyLanguages.Count == 0)
        {
            return;
        }

        foreach (var language in melodyLanguages)
        {
            var newLanguage = await GetOrCreateLanguage(db, language.Value.Code, language.Value.Name);
            await SeedVocalMusicReleasesForLanguage(db, mediaReader, language.Value.Code, newLanguage, displayLanguage);
        }
    }

    private async Task SeedVocalMusicReleasesForLanguage(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageCode,
        Language newLanguage,
        Language displayLanguage)
    {
        var vocalMusicReleases = await GetSafely(() => mediaReader.GetVocalMusicReleases(languageCode));
        if (vocalMusicReleases == null || vocalMusicReleases.Count == 0)
        {
            return;
        }

        // Load existing vocal music codes for this language upfront (one query)
        var existingCodesList = await db.VocalMusic
            .Where(v => v.Language.Code == languageCode)
            .Select(v => v.Code)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        foreach (var vocalMusicRelease in vocalMusicReleases)
        {
            // Skip if already exists (in-memory check, fast)
            if (existingCodes.Contains(vocalMusicRelease.Value.Code))
            {
                logger.Information("Skipping song section {SongPublicationName} ({SongPublicationCode}) for language {LanguageCode} - already exists",
                    vocalMusicRelease.Value.Name, vocalMusicRelease.Value.Code, languageCode);
                continue;
            }

            logger.Information("Seeding song section {SongPublicationName} ({SongPublicationCode}) for language {LanguageCode}",
                vocalMusicRelease.Value.Name, vocalMusicRelease.Value.Code, languageCode);

            var tracks = await GetSafely(() => mediaReader.GetVocalMusicTracks(languageCode, vocalMusicRelease.Key));
            if (tracks == null || tracks.Count == 0)
            {
                continue;
            }

            var newVocalMusic = CreateVocalMusic(vocalMusicRelease.Value, newLanguage, displayLanguage);
            await AddTracksToVocalMusic(db, tracks, newVocalMusic);

            await db.VocalMusic.AddAsync(newVocalMusic);
            await db.SaveChangesAsync();
        }
    }

    private static VocalMusic CreateVocalMusic(Publication vocalMusicRelease, Language newLanguage, Language displayLanguage)
    {
        return new VocalMusic
        {
            Code = vocalMusicRelease.Code,
            Name = vocalMusicRelease.Name,
            DisplayLanguage = displayLanguage,
            Language = newLanguage
        };
    }

    private async Task AddTracksToVocalMusic(MediaDbContext db, SortedDictionary<int, MusicTrack> tracks, VocalMusic newVocalMusic)
    {
        foreach (var track in tracks)
        {
            var audioSource = await CreateAudioSource(db, track.Value.Url);
            var newTrack = new Shared.Models.Media.Music.MusicTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Source = audioSource
            };

            newVocalMusic.Tracks.Add(newTrack);
        }
    }
}
