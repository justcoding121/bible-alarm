#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using BiblePublication = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using BiblePublicationSection = Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications.BiblePublicationSection;
using BiblePublicationTrack = Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications.BiblePublicationTrack;
using DramaTrack = Bible.Alarm.AudioLinksHarvestor.Models.Drama.DramaTrack;
using MusicTrack = Bible.Alarm.AudioLinksHarvestor.Models.Music.MusicTrack;
using Publication = Bible.Alarm.AudioLinksHarvestor.Models.Publication;
using VideoEpisode = Bible.Alarm.AudioLinksHarvestor.Models.Video.VideoEpisode;

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

internal class DbSeeder(ILogger logger, IServiceScopeFactory scopeFactory, DownloadUtility downloadUtility)
{

    public async Task Seed()
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = DELETE;");
            await db.Database.MigrateAsync();
        }

        // Seed default Categories and ApiUrls first
        await SeedDefaultCategoriesAndApiUrls();

        var indexDir = DirectoryHelper.IndexDirectory;
        var mediaDir = Path.Combine(indexDir, "media");
        
        // Seed in order to maximize language table population before Drama
        // Bible, Music, and Video all extract direction from their APIs
        await SeedBiblePublications(mediaDir);
        await SeedMelodies(mediaDir);
        await SeedVocals(mediaDir);
        await SeedVideos(mediaDir);
        await SeedDramas(mediaDir);  // Last - can rely on existing languages
    }

    private async Task SeedDefaultCategoriesAndApiUrls()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Seed Categories
        var categories = new[]
        {
            new Category { CategoryName = "Bible" },
            new Category { CategoryName = "Dramas" },
            new Category { CategoryName = "Music" }
        };

        foreach (var category in categories)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.CategoryName == category.CategoryName);
            if (existing == null)
            {
                db.Categories.Add(category);
                logger.Information("Seeding category: {CategoryName}", category.CategoryName);
            }
        }

        // Seed ApiUrls
        // Note: PathPrefix has a unique constraint, so we check by PathPrefix only
        // Both URLs use the same PathPrefix, so we only need to seed one
        var pathPrefix = "apis/pub-media/GETPUBMEDIALINKS";
        var existingApiUrl = await db.BaseUrls.FirstOrDefaultAsync(a => a.PathPrefix == pathPrefix);
        
        if (existingApiUrl == null)
        {
            // Use the primary URL (b.jw-cdn.org) as the default
            var apiUrl = new BaseUrl
            {
                Url = "https://b.jw-cdn.org",
                PathPrefix = pathPrefix
            };
            
            db.BaseUrls.Add(apiUrl);
            await db.SaveChangesAsync(); // Save to get the ID
            
            // Seed UrlParam with output=json for this base URL
            var urlParam = new UrlParam
            {
                BaseUrlId = apiUrl.Id,
                Key = "output",
                Value = "json",
                IsQueryParam = true
            };
            db.UrlParams.Add(urlParam);
            
            logger.Information("Seeding ApiUrl: {BaseUrl} / {PathPrefix} with output=json", apiUrl.Url, apiUrl.PathPrefix);
        }
        else
        {
            // Check if output=json param already exists for this base URL
            var existingParam = await db.UrlParams.FirstOrDefaultAsync(p => 
                p.BaseUrlId == existingApiUrl.Id && p.Key == "output" && p.Value == "json");
            if (existingParam == null)
            {
                var urlParam = new UrlParam
                {
                    BaseUrlId = existingApiUrl.Id,
                    Key = "output",
                    Value = "json",
                    IsQueryParam = true
                };
                db.UrlParams.Add(urlParam);
                logger.Information("Adding output=json param to existing ApiUrl: {BaseUrl} / {PathPrefix}", existingApiUrl.Url, existingApiUrl.PathPrefix);
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task SeedBiblePublications(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var mediaReader = new MediaReader(indexDir);

        var bibleLanguages = await GetBibleLanguagesSafely(mediaReader);
        if (bibleLanguages == null || bibleLanguages.Count == 0)
        {
            return;
        }

        foreach (var language in bibleLanguages)
        {
            var newLanguage = await GetOrCreateLanguage(db, language.Value.Code, language.Value.Name, language.Value.Direction);
            await SeedPublicationsForLanguage(db, mediaReader, language.Key, newLanguage);
        }
    }

    private async Task<Language> GetOrCreateLanguage(MediaDbContext db, string code, string name, string direction = "ltr")
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
                Name = name,
                Direction = direction
            };
        }
        else if (language.Direction != direction && direction != "ltr")
        {
            // Update direction if it's explicitly set (non-default)
            language.Direction = direction;
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
            // Language not found - fetch direction from Mediator API
            var (name, direction) = await FetchLanguageInfoFromApi(normalizedCode);
            
            language = new Language
            {
                Code = normalizedCode,
                Name = name ?? normalizedCode, // Use fetched name or code as fallback
                Direction = direction
            };
            db.Languages.Add(language);
            await db.SaveChangesAsync();
            
            logger.Information("Created new language {Code} with direction {Direction} from API", normalizedCode, direction);
        }
        return language;
    }

    /// <summary>
    /// Fetches language name and direction from the Mediator API.
    /// Uses the Dramas category as it's commonly available across languages.
    /// </summary>
    private async Task<(string? Name, string Direction)> FetchLanguageInfoFromApi(string languageCode)
    {
        try
        {
            // Use Dramas category to fetch language info
            var url = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{languageCode}/Dramas?detailed=1";
            var jsonString = await downloadUtility.GetAsync(url);

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("language", out var langElement))
            {
                var direction = "ltr";
                if (langElement.TryGetProperty("direction", out var dirElement))
                {
                    direction = dirElement.GetString() ?? "ltr";
                }

                string? name = null;
                if (langElement.TryGetProperty("name", out var nameElement))
                {
                    var rawName = nameElement.GetString();
                    // Decode HTML entities like &nbsp; to proper characters
                    name = rawName != null ? WebUtility.HtmlDecode(rawName) : null;
                }

                return (name, direction);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to fetch language info for {LanguageCode} from API, using default ltr", languageCode);
        }

        return (null, "ltr");
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
        Language newLanguage)
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

            var category = await GetCategory(db, "Bible");
            var biblePublication = await CreateBiblePublication(db, publication.Value, newLanguage, category.Id, languageKey, isVideo: false);
            await SeedSectionsForPublication(db, mediaReader, languageKey, publication.Key, sections, biblePublication);

            await db.BiblePublications.AddAsync(biblePublication);
            await db.SaveChangesAsync();
        }
    }

    private async Task<List<BaseUrl>> GetAllBaseUrls(MediaDbContext db)
    {
        return await db.BaseUrls
            .Where(x => x.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .ToListAsync();
    }

    private async Task<Category> GetCategory(MediaDbContext db, string categoryName)
    {
        return await db.Categories.FirstAsync(x => x.CategoryName == categoryName);
    }

    private async Task<BiblePublication> CreateBiblePublication(
        MediaDbContext db,
        Publication publication, 
        Language? newLanguage,
        int categoryId,
        string? languageCode,
        bool isVideo = false)
    {
        // Get all base URLs (both https://b.jw-cdn.org and https://app.jw-cdn.org)
        var baseUrls = await GetAllBaseUrls(db);
        
        // Create UrlParam entries for BiblePublication
        var urlParams = new List<UrlParam>
        {
            new UrlParam
            {
                BiblePublicationId = 0, // Will be set after publication is saved
                Key = "pub",
                Value = publication.Code,
                IsQueryParam = true
            },
            new UrlParam
            {
                BiblePublicationId = 0, // Will be set after publication is saved
                Key = "fileformat",
                Value = isVideo ? "mp4" : "mp3",
                IsQueryParam = true
            }
        };

        // Add langwritten parameter only if language is provided (vocals have language, melodies don't)
        if (!string.IsNullOrEmpty(languageCode))
        {
            urlParams.Add(new UrlParam
            {
                BiblePublicationId = 0, // Will be set after publication is saved
                Key = "langwritten",
                Value = languageCode,
                IsQueryParam = true
            });
        }

        // Ensure at least one BaseUrl is linked (required relationship)
        if (baseUrls == null || baseUrls.Count == 0)
        {
            throw new InvalidOperationException($"No BaseUrls found for publication {publication.Code}. At least one BaseUrl is required.");
        }

        var biblePublication = new BiblePublication
        {
            Name = publication.Name,
            Code = publication.Code,
            Language = newLanguage, // Optional - can be null
            CategoryId = categoryId,
            BaseUrls = baseUrls, // Required - must have at least one
            UrlParams = urlParams, // Optional - can be empty
            IsVideo = isVideo
        };

        return biblePublication;
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
            // Create UrlParam for section with booknum
            var sectionUrlParam = new UrlParam
            {
                BiblePublicationSectionId = 0, // Will be set after section is saved
                Key = "booknum",
                Value = section.Value.Number.ToString(),
                IsQueryParam = true
            };

            var newSection = new Shared.Models.Media.BiblePublications.BiblePublicationSection
            {
                Name = section.Value.Name,
                Number = section.Value.Number,
                UrlParams = new List<UrlParam> { sectionUrlParam }
            };

            biblePublication.Sections.Add(newSection);

            var tracks = await GetSafely(() => mediaReader.GetBiblePublicationTracks(languageKey, publicationKey, section.Key));
            if (tracks == null || tracks.Count == 0)
            {
                continue;
            }

            await AddTracksToSection(db, tracks, newSection, biblePublication);
        }
    }

    private async Task AddTracksToSection(
        MediaDbContext db,
        SortedDictionary<int, BiblePublicationTrack> tracks,
        Shared.Models.Media.BiblePublications.BiblePublicationSection newSection,
        BiblePublication biblePublication)
    {
        foreach (var track in tracks)
        {
            // Create UrlParam entries for track
            var trackUrlParams = new List<UrlParam>
            {
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "pub",
                    Value = biblePublication.Code,
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "track",
                    Value = track.Value.Number.ToString(),
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "fileformat",
                    Value = biblePublication.IsVideo ? "mp4" : "mp3",
                    IsQueryParam = true
                }
            };

            var newTrack = new Shared.Models.Media.BiblePublications.BiblePublicationTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title, // Localized chapter title (e.g., "അധ്യായം 1" in Malayalam)
                Publication = biblePublication,
                Section = newSection,
                UrlParams = trackUrlParams
            };

            newSection.Tracks.Add(newTrack);
        }
    }

    #region Drama Seeding

    private async Task SeedDramas(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

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
            await SeedDramaPublicationsForLanguage(db, mediaReader, language.Key, newLanguage);
        }
    }

    private async Task SeedDramaPublicationsForLanguage(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageCode,
        Language language)
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

            var category = await GetCategory(db, "Dramas");
            var newPublication = await CreateBiblePublication(db, publication.Value, language, category.Id, languageCode, isVideo: false);

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
            // Create UrlParam entries for track
            var trackUrlParams = new List<UrlParam>
            {
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "pub",
                    Value = publication.Code,
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "track",
                    Value = track.Value.Number.ToString(),
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "fileformat",
                    Value = publication.IsVideo ? "mp4" : "mp3",
                    IsQueryParam = true
                }
            };

            var newTrack = new Shared.Models.Media.BiblePublications.BiblePublicationTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Publication = publication,
                UrlParams = trackUrlParams
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
            await SeedVideoPublicationsForLanguage(db, mediaReader, language.Key, newLanguage);
        }
    }

    private async Task SeedVideoPublicationsForLanguage(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageCode,
        Language language)
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

            // Get the first BaseUrl (both should be seeded by SeedDefaultCategoriesAndApiUrls)
            var apiUrls = await GetAllBaseUrls(db);
            var apiUrl = apiUrls.FirstOrDefault();
            if (apiUrl == null)
            {
                logger.Warning("No BaseUrl found for video publication. BaseUrls should be seeded first.");
                continue;
            }
            var category = await GetCategory(db, "Dramas"); // Videos use Dramas category
            var newPublication = await CreateBiblePublication(db, publication.Value, language, category.Id, languageCode, isVideo: true);

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
            // Create UrlParam entries for track
            var trackUrlParams = new List<UrlParam>
            {
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "pub",
                    Value = publication.Code,
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "track",
                    Value = episode.Value.Number.ToString(),
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "fileformat",
                    Value = publication.IsVideo ? "mp4" : "mp3",
                    IsQueryParam = true
                }
            };

            var newTrack = new Shared.Models.Media.BiblePublications.BiblePublicationTrack
            {
                Number = episode.Value.Number,
                Title = episode.Value.Title,
                Publication = publication,
                UrlParams = trackUrlParams
            };

            publication.Tracks.Add(newTrack);
        }
    }

    #endregion

    private async Task SeedMelodies(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var mediaReader = new MediaReader(indexDir);

        var melodyMusicReleases = await GetSafely(() => mediaReader.GetMelodyMusicReleases());
        if (melodyMusicReleases == null || melodyMusicReleases.Count == 0)
        {
            return;
        }

        // Load existing melody codes upfront (one query) - using BiblePublication with Category="Music" and LanguageId=null
        var category = await GetCategory(db, "Music");
        var existingCodesList = await db.BiblePublications
            .Where(p => p.CategoryId == category.Id && p.LanguageId == null)
            .Select(p => p.Code)
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

            // Create BiblePublication for melody (no language, Category="Music")
            var biblePublication = await CreateBiblePublication(db, melodyMusicRelease.Value, null, category.Id, null, isVideo: false);
            await AddTracksToMelodyMusic(db, tracks, biblePublication);

            await db.BiblePublications.AddAsync(biblePublication);
            await db.SaveChangesAsync();
        }
    }

    private async Task AddTracksToMelodyMusic(MediaDbContext db, SortedDictionary<int, MusicTrack> tracks, BiblePublication biblePublication)
    {
        foreach (var track in tracks)
        {
            // Create UrlParam entries for track
            // For melodies, DownloadCode (e.g., "iam-1", "iam-2") should be stored as pub parameter in UrlParam
            // OriginalTrackNumber should be stored as track parameter if it differs from Number
            var trackUrlParams = new List<UrlParam>();

            // Store disc code (DownloadCode) as pub parameter if it differs from publication code
            if (!string.IsNullOrEmpty(track.Value.DownloadCode) && track.Value.DownloadCode != biblePublication.Code)
            {
                trackUrlParams.Add(new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "pub",
                    Value = track.Value.DownloadCode, // e.g., "iam-1", "iam-2"
                    IsQueryParam = true
                });
            }
            else
            {
                // Use publication code as pub parameter
                trackUrlParams.Add(new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "pub",
                    Value = biblePublication.Code,
                    IsQueryParam = true
                });
            }

            // Store track number - use OriginalTrackNumber if available, otherwise use Number
            var trackNumber = track.Value.OriginalTrackNumber ?? track.Value.Number;
            trackUrlParams.Add(new UrlParam
            {
                BiblePublicationTrackId = 0,
                Key = "track",
                Value = trackNumber.ToString(),
                IsQueryParam = true
            });

            // Add fileformat parameter
            trackUrlParams.Add(new UrlParam
            {
                BiblePublicationTrackId = 0,
                Key = "fileformat",
                Value = "mp3",
                IsQueryParam = true
            });

            var newTrack = new Shared.Models.Media.BiblePublications.BiblePublicationTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Publication = biblePublication,
                UrlParams = trackUrlParams
            };

            biblePublication.Tracks.Add(newTrack);
        }
    }

    private async Task SeedVocals(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var mediaReader = new MediaReader(indexDir);

        var melodyLanguages = await GetSafely(() => mediaReader.GetVocalMusicLanguages());
        if (melodyLanguages == null || melodyLanguages.Count == 0)
        {
            return;
        }

        foreach (var language in melodyLanguages)
        {
            var newLanguage = await GetOrCreateLanguage(db, language.Value.Code, language.Value.Name, language.Value.Direction);
            await SeedVocalMusicReleasesForLanguage(db, mediaReader, language.Value.Code, newLanguage);
        }
    }

    private async Task SeedVocalMusicReleasesForLanguage(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageCode,
        Language newLanguage)
    {
        var vocalMusicReleases = await GetSafely(() => mediaReader.GetVocalMusicReleases(languageCode));
        if (vocalMusicReleases == null || vocalMusicReleases.Count == 0)
        {
            return;
        }

        // Load existing vocal music codes for this language upfront (one query) - using BiblePublication with Category="Music" and LanguageId set
        var category = await GetCategory(db, "Music");
        var existingCodesList = await db.BiblePublications
            .Where(p => p.CategoryId == category.Id && p.LanguageId == newLanguage.Id)
            .Select(p => p.Code)
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

            // Create BiblePublication for vocal music (with language, Category="Music")
            var biblePublication = await CreateBiblePublication(db, vocalMusicRelease.Value, newLanguage, category.Id, languageCode, isVideo: false);
            await AddTracksToVocalMusic(db, tracks, biblePublication);

            await db.BiblePublications.AddAsync(biblePublication);
            await db.SaveChangesAsync();
        }
    }

    private async Task AddTracksToVocalMusic(MediaDbContext db, SortedDictionary<int, MusicTrack> tracks, BiblePublication biblePublication)
    {
        foreach (var track in tracks)
        {
            // Create UrlParam entries for track
            // For vocals, DownloadCode is typically the same as publication code
            // OriginalTrackNumber is typically the same as Number for vocal music
            var trackUrlParams = new List<UrlParam>
            {
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "pub",
                    Value = biblePublication.Code,
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "track",
                    Value = (track.Value.OriginalTrackNumber ?? track.Value.Number).ToString(),
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "fileformat",
                    Value = "mp3",
                    IsQueryParam = true
                }
            };

            // Add langwritten parameter for vocals (they have language)
            if (biblePublication.LanguageId.HasValue && biblePublication.Language != null)
            {
                trackUrlParams.Add(new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "langwritten",
                    Value = biblePublication.Language.Code,
                    IsQueryParam = true
                });
            }

            var newTrack = new Shared.Models.Media.BiblePublications.BiblePublicationTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Publication = biblePublication,
                UrlParams = trackUrlParams
            };

            biblePublication.Tracks.Add(newTrack);
        }
    }
}
