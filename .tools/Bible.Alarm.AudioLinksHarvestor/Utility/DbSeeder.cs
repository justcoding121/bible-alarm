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

internal class DbSeeder : IDataPersister
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly DownloadUtility downloadUtility;
    private readonly InMemoryDataStore dataStore;

    public DbSeeder(ILogger logger, IServiceScopeFactory scopeFactory, DownloadUtility downloadUtility)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;
        this.downloadUtility = downloadUtility;
        this.dataStore = new InMemoryDataStore();
    }

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
        
        // Seed in order to maximize language table population before Drama
        // Bible, Music, and Video all extract direction from their APIs
        // Only languages with publications are tracked in the database
        await SeedBiblePublicationsFromMemory();
        await SeedMelodiesFromMemory();
        await SeedVocalsFromMemory();
        await SeedVideosFromMemory();
        await SeedDramasFromMemory();  // Last - can rely on existing languages
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
        var apiUrls = new[]
        {
            new BaseUrl
            {
                Url = "https://b.jw-cdn.org",
                PathPrefix = "apis/pub-media/GETPUBMEDIALINKS"
            },
            new BaseUrl
            {
                Url = "https://app.jw-cdn.org",
                PathPrefix = "apis/pub-media/GETPUBMEDIALINKS"
            }
        };

        foreach (var apiUrl in apiUrls)
        {
            // Check if this specific URL and PathPrefix combination already exists
            var existingApiUrl = await db.BaseUrls.FirstOrDefaultAsync(a => 
                a.Url == apiUrl.Url && a.PathPrefix == apiUrl.PathPrefix);
            if (existingApiUrl == null)
            {
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
        }

        await db.SaveChangesAsync();
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

    private async Task<T?> GetSafely<T>(Func<Task<T>> getter, string? context = null) where T : class
    {
        try
        {
            return await getter();
        }
        catch (FileNotFoundException ex)
        {
            if (!string.IsNullOrEmpty(context))
            {
                logger.Warning("File not found for {Context}: {FileName}", context, ex.FileName);
            }
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            if (!string.IsNullOrEmpty(context))
            {
                logger.Warning("Directory not found for {Context}: {DirectoryName}", context, ex.Message);
            }
            return null;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(context))
            {
                logger.Error(ex, "Error in {Context}", context);
            }
            return null;
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

        var biblePublication = new BiblePublication
        {
            Name = publication.Name,
            Code = publication.Code,
            Language = newLanguage, // Optional - can be null
            CategoryId = categoryId,
            UrlParams = urlParams, // Optional - can be empty
            IsVideo = isVideo
        };

        return biblePublication;
    }

    #region Drama Seeding (file-based methods removed - using memory-based seeding)

    private async Task AddDramaTracksToSection(
        MediaDbContext db,
        SortedDictionary<int, DramaTrack> tracks,
        Shared.Models.Media.BiblePublications.BiblePublicationSection newSection,
        BiblePublication biblePublication,
        string languageCode)
    {
        // Get section code from section's UrlParams
        var sectionCode = newSection.UrlParams.FirstOrDefault(p => p.Key == "sectionCode")?.Value ?? biblePublication.Code;
        
        foreach (var track in tracks)
        {
            // Create UrlParam entries for track
            // Drama tracks use GETPUBMEDIALINKS format: ?output=json&pub={sectionCode}&fileformat=MP3&langwritten={languageCode}&track={trackNumber}
            var trackUrlParams = new List<UrlParam>
            {
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "pub",
                    Value = sectionCode,
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
                    Value = "mp3",
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "langwritten",
                    Value = languageCode,
                    IsQueryParam = true
                }
            };

            var newTrack = new Shared.Models.Media.BiblePublications.BiblePublicationTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Publication = biblePublication,
                Section = newSection,
                UrlParams = trackUrlParams
            };
            newSection.Tracks.Add(newTrack);
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

    #region Video Seeding (file-based methods removed - using memory-based seeding)

    #endregion


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

    private async Task AddTracksToMelodyMusicWithSections(
        MediaDbContext db,
        Dictionary<string, (string Name, SortedDictionary<int, MusicTrack> Tracks)> discTracksMap,
        BiblePublication biblePublication)
    {
        int sectionNumber = 1;
        foreach (var discEntry in discTracksMap.OrderBy(d => d.Key))
        {
            var discCode = discEntry.Key;
            var discName = discEntry.Value.Name;
            var tracks = discEntry.Value.Tracks;

            // Create a section for this disc
            // Use disc code as section identifier (e.g., "iam-1", "iam-2")
            var sectionUrlParam = new UrlParam
            {
                BiblePublicationSectionId = 0, // Will be set after section is saved
                Key = "booknum",
                Value = sectionNumber.ToString(),
                IsQueryParam = true
            };

            var newSection = new Shared.Models.Media.BiblePublications.BiblePublicationSection
            {
                Name = discName, // Use disc name as section name
                Number = sectionNumber,
                UrlParams = new List<UrlParam> { sectionUrlParam }
            };

            biblePublication.Sections.Add(newSection);

            // Add tracks to this section
            foreach (var track in tracks)
            {
                // Create UrlParam entries for track
                var trackUrlParams = new List<UrlParam>();

                // Store disc code (DownloadCode) as pub parameter
                trackUrlParams.Add(new UrlParam
                {
                    BiblePublicationTrackId = 0, // Will be set after track is saved
                    Key = "pub",
                    Value = discCode, // e.g., "iam-1", "iam-2"
                    IsQueryParam = true
                });

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
                    Section = newSection,
                    UrlParams = trackUrlParams
                };

                newSection.Tracks.Add(newTrack);
            }

            sectionNumber++;
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

    #region IDataPersister Implementation

    public Task SaveBiblePublicationSections(
        string languageCode,
        string publicationCode,
        Dictionary<int, BiblePublicationSection> sections,
        Dictionary<int, Dictionary<int, BiblePublicationTrack>> sectionNumberTrackMap)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToUpperInvariant());
        dataStore.BiblePublications[key] = (sections, sectionNumberTrackMap);
        return Task.CompletedTask;
    }

    public Task SaveDramaPublication(
        string languageCode,
        string publicationCode,
        string publicationName,
        Dictionary<string, List<DramaTrack>> tracksBySection,
        Dictionary<string, string> sectionNames)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToUpperInvariant());
        dataStore.DramaPublications[key] = (publicationName, tracksBySection, sectionNames);
        return Task.CompletedTask;
    }

    public Task SaveMusicTracks(
        string publicationCode,
        string? languageCode,
        List<MusicTrack> tracks)
    {
        var key = (publicationCode.ToUpperInvariant(), languageCode?.ToUpperInvariant());
        dataStore.MusicTracks[key] = tracks;
        return Task.CompletedTask;
    }

    public Task SaveMelodyMusicTracks(
        string publicationCode,
        Dictionary<string, List<MusicTrack>> discTracksMap,
        Dictionary<string, string> discNamesMap)
    {
        dataStore.MelodyMusic[publicationCode.ToUpperInvariant()] = (discTracksMap, discNamesMap);
        return Task.CompletedTask;
    }

    public Task SaveVideoEpisodes(
        string languageCode,
        string publicationCode,
        string publicationName,
        List<VideoEpisode> episodes)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToUpperInvariant());
        dataStore.VideoPublications[key] = (publicationName, episodes);
        return Task.CompletedTask;
    }

    public Task SaveLanguageDiscovery(
        string languageCode,
        string publicationCode,
        Dictionary<string, string> languageCodeToNameMapping)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToUpperInvariant());
        dataStore.LanguageDiscovery[key] = languageCodeToNameMapping;
        return Task.CompletedTask;
    }

    #endregion

    #region Seed from Memory Methods

    private async Task SeedBiblePublicationsFromMemory()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Get all unique language codes from stored Bible publications
        var languageCodes = dataStore.BiblePublications.Keys.Select(k => k.LanguageCode).Distinct().ToList();
        logger.Information("Found {Count} Bible languages to seed.", languageCodes.Count);

        foreach (var languageCode in languageCodes)
        {
            var newLanguage = await GetOrCreateLanguageByCode(db, languageCode);
            await SeedBiblePublicationsForLanguageFromMemory(db, languageCode, newLanguage);
        }
    }

    private async Task SeedBiblePublicationsForLanguageFromMemory(
        MediaDbContext db,
        string languageCode,
        Language language)
    {
        var publications = dataStore.BiblePublications
            .Where(kvp => kvp.Key.LanguageCode == languageCode)
            .ToList();

        if (publications.Count == 0)
        {
            return;
        }

        logger.Information("Found {Count} Bible publication(s) for language {LanguageCode}", publications.Count, languageCode);

        // Load existing publication codes for this language upfront
        var existingCodesList = await db.BiblePublications
            .Where(t => t.Language.Code == languageCode)
            .Select(t => t.Code)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        foreach (var (key, (sections, sectionNumberTrackMap)) in publications)
        {
            // Extract publication code from key
            var publicationCode = key.PublicationCode;
            
            // Skip if already exists
            if (existingCodes.Contains(publicationCode))
            {
                logger.Information("Skipping publication {PublicationCode} for language {LanguageCode} - already exists",
                    publicationCode, languageCode);
                continue;
            }

            // Get publication name from first section or use code as fallback
            var publicationName = sections.Values.FirstOrDefault()?.Name ?? publicationCode;

            logger.Information("Seeding publication {PublicationName} ({PublicationCode}) for language {LanguageCode}",
                publicationName, publicationCode, languageCode);

            var category = await GetCategory(db, "Bible");
            var publication = new Publication { Code = publicationCode, Name = publicationName };
            var biblePublication = await CreateBiblePublication(db, publication, language, category.Id, languageCode, isVideo: false);
            
            await SeedSectionsForPublicationFromMemory(db, sections, sectionNumberTrackMap, biblePublication);

            await db.BiblePublications.AddAsync(biblePublication);
            await db.SaveChangesAsync();
        }
    }

    private async Task SeedSectionsForPublicationFromMemory(
        MediaDbContext db,
        Dictionary<int, BiblePublicationSection> sections,
        Dictionary<int, Dictionary<int, BiblePublicationTrack>> sectionNumberTrackMap,
        BiblePublication biblePublication)
    {
        foreach (var section in sections)
        {
            var sectionUrlParam = new UrlParam
            {
                BiblePublicationSectionId = 0,
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

            if (sectionNumberTrackMap.TryGetValue(section.Key, out var tracks) && tracks != null && tracks.Count > 0)
            {
                await AddTracksToSectionFromMemory(db, tracks, newSection, biblePublication);
            }
        }
    }

    private async Task AddTracksToSectionFromMemory(
        MediaDbContext db,
        Dictionary<int, BiblePublicationTrack> tracks,
        Shared.Models.Media.BiblePublications.BiblePublicationSection newSection,
        BiblePublication biblePublication)
    {
        foreach (var track in tracks.OrderBy(t => t.Key))
        {
            var trackUrlParams = new List<UrlParam>
            {
                new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "booknum",
                    Value = newSection.Number.ToString(),
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "track",
                    Value = track.Value.Number.ToString(),
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

            var newTrack = new Shared.Models.Media.BiblePublications.BiblePublicationTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Publication = biblePublication,
                Section = newSection,
                UrlParams = trackUrlParams
            };

            newSection.Tracks.Add(newTrack);
        }
    }

    private async Task SeedDramasFromMemory()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var languageCodes = dataStore.DramaPublications.Keys.Select(k => k.LanguageCode).Distinct().ToList();
        logger.Information("Found {Count} drama languages to seed.", languageCodes.Count);

        foreach (var languageCode in languageCodes)
        {
            var newLanguage = await GetOrCreateLanguageByCode(db, languageCode);
            await SeedDramaPublicationsForLanguageFromMemory(db, languageCode, newLanguage);
        }
    }

    private async Task SeedDramaPublicationsForLanguageFromMemory(
        MediaDbContext db,
        string languageCode,
        Language language)
    {
        var publications = dataStore.DramaPublications
            .Where(kvp => kvp.Key.LanguageCode == languageCode)
            .ToList();

        if (publications.Count == 0)
        {
            return;
        }

        var existingCodesList = await db.BiblePublications
            .Where(p => p.Language.Code == languageCode)
            .Select(p => p.Code)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        foreach (var (key, (publicationName, tracksBySection, sectionNames)) in publications)
        {
            var publicationCode = key.PublicationCode;
            
            if (existingCodes.Contains(publicationCode))
            {
                logger.Information("Skipping drama publication {PublicationName} ({PublicationCode}) for language {LanguageCode} - already exists",
                    publicationName, publicationCode, languageCode);
                continue;
            }

            logger.Information("Seeding drama publication {PublicationName} ({PublicationCode}) for language {LanguageCode}",
                publicationName, publicationCode, languageCode);

            var category = await GetCategory(db, "Dramas");
            var publication = new Publication { Code = publicationCode, Name = publicationName };
            var newPublication = await CreateBiblePublication(db, publication, language, category.Id, languageCode, isVideo: false);

            await SeedDramaSectionsForPublicationFromMemory(db, tracksBySection, sectionNames, newPublication, languageCode);

            await db.BiblePublications.AddAsync(newPublication);
            await db.SaveChangesAsync();
        }
    }

    private async Task SeedDramaSectionsForPublicationFromMemory(
        MediaDbContext db,
        Dictionary<string, List<DramaTrack>> tracksBySection,
        Dictionary<string, string> sectionNames,
        BiblePublication biblePublication,
        string languageCode)
    {
        int sectionNumber = 1;
        foreach (var sectionEntry in tracksBySection.OrderBy(s => s.Key))
        {
            var sectionCode = sectionEntry.Key;
            var tracks = sectionEntry.Value;
            var sectionName = sectionNames.TryGetValue(sectionCode, out var name) && !string.IsNullOrEmpty(name) ? name : sectionCode;

            var sectionUrlParams = new List<UrlParam>
            {
                new UrlParam
                {
                    BiblePublicationSectionId = 0,
                    Key = "sectionCode",
                    Value = sectionCode,
                    IsQueryParam = false
                }
            };

            var newSection = new Shared.Models.Media.BiblePublications.BiblePublicationSection
            {
                Name = sectionName,
                Number = sectionNumber,
                UrlParams = sectionUrlParams
            };

            biblePublication.Sections.Add(newSection);

            if (tracks != null && tracks.Count > 0)
            {
                var sortedTracks = new SortedDictionary<int, DramaTrack>();
                foreach (var track in tracks.OrderBy(t => t.Number))
                {
                    sortedTracks[track.Number] = track;
                }
                await AddDramaTracksToSection(db, sortedTracks, newSection, biblePublication, languageCode);
            }

            sectionNumber++;
        }
    }

    private async Task SeedMelodiesFromMemory()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        if (dataStore.MelodyMusic.Count == 0)
        {
            return;
        }

        logger.Information("Seeding melody code iam music to database.");

        var category = await GetCategory(db, "Music");
        var publication = new Publication { Code = "iam", Name = "Kingdom Melodies" };
        var language = await GetOrCreateLanguageByCode(db, "E"); // Melody music uses English as base

        var biblePublication = await CreateBiblePublication(db, publication, language, category.Id, "E", isVideo: false);

        var discTracksMap = new Dictionary<string, (string Name, SortedDictionary<int, MusicTrack> Tracks)>();
        foreach (var (pubCode, (discTracks, discNames)) in dataStore.MelodyMusic)
        {
            foreach (var discEntry in discTracks)
            {
                var discCode = discEntry.Key;
                var discName = discNames.TryGetValue(discCode, out var name) ? name : discCode;
                var sortedTracks = new SortedDictionary<int, MusicTrack>();
                foreach (var track in discEntry.Value.OrderBy(t => t.Number))
                {
                    sortedTracks[track.Number] = track;
                }
                discTracksMap[discCode] = (discName, sortedTracks);
            }
        }

        await AddTracksToMelodyMusicWithSections(db, discTracksMap, biblePublication);

        await db.BiblePublications.AddAsync(biblePublication);
        await db.SaveChangesAsync();
    }

    private async Task SeedVocalsFromMemory()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var languageCodes = dataStore.MusicTracks.Keys
            .Where(k => k.LanguageCode != null)
            .Select(k => k.LanguageCode!)
            .Distinct()
            .ToList();

        logger.Information("Found {Count} vocal music languages to seed.", languageCodes.Count);

        foreach (var languageCode in languageCodes)
        {
            var newLanguage = await GetOrCreateLanguageByCode(db, languageCode);
            await SeedVocalMusicReleasesForLanguageFromMemory(db, languageCode, newLanguage);
        }
    }

    private async Task SeedVocalMusicReleasesForLanguageFromMemory(
        MediaDbContext db,
        string languageCode,
        Language language)
    {
        var publications = dataStore.MusicTracks
            .Where(kvp => kvp.Key.LanguageCode == languageCode)
            .ToList();

        if (publications.Count == 0)
        {
            return;
        }

        logger.Information("Found {Count} vocal music publication(s) for language {LanguageCode}", publications.Count, languageCode);

        // Map publication codes to names (would need to be stored separately or extracted)
        // For now, using code as name
        var category = await GetCategory(db, "Music");
        var existingCodesList = await db.BiblePublications
            .Where(p => p.Language.Code == languageCode && p.Category.CategoryName == "Music")
            .Select(p => p.Code)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        foreach (var (key, tracks) in publications)
        {
            var publicationCode = key.PublicationCode;
            
            if (existingCodes.Contains(publicationCode))
            {
                continue;
            }

            // Get publication name from mapping (would need to be stored)
            var publicationName = publicationCode; // Fallback
            var publication = new Publication { Code = publicationCode, Name = publicationName };
            var biblePublication = await CreateBiblePublication(db, publication, language, category.Id, languageCode, isVideo: false);

            await AddVocalTracksToPublicationFromMemory(db, tracks, biblePublication);

            await db.BiblePublications.AddAsync(biblePublication);
            await db.SaveChangesAsync();
        }
    }

    private async Task AddVocalTracksToPublicationFromMemory(
        MediaDbContext db,
        List<MusicTrack> tracks,
        BiblePublication biblePublication)
    {
        foreach (var track in tracks.OrderBy(t => t.Number))
        {
            var trackUrlParams = new List<UrlParam>
            {
                new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "pub",
                    Value = biblePublication.Code,
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "track",
                    Value = track.Number.ToString(),
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
                Number = track.Number,
                Title = track.Title,
                Publication = biblePublication,
                UrlParams = trackUrlParams
            };

            biblePublication.Tracks.Add(newTrack);
        }
    }

    private async Task SeedVideosFromMemory()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var languageCodes = dataStore.VideoPublications.Keys.Select(k => k.LanguageCode).Distinct().ToList();
        logger.Information("Found {Count} video languages to seed.", languageCodes.Count);

        foreach (var languageCode in languageCodes)
        {
            var newLanguage = await GetOrCreateLanguageByCode(db, languageCode);
            await SeedVideoPublicationsForLanguageFromMemory(db, languageCode, newLanguage);
        }
    }

    private async Task SeedVideoPublicationsForLanguageFromMemory(
        MediaDbContext db,
        string languageCode,
        Language language)
    {
        var publications = dataStore.VideoPublications
            .Where(kvp => kvp.Key.LanguageCode == languageCode)
            .ToList();

        if (publications.Count == 0)
        {
            return;
        }

        var existingCodesList = await db.BiblePublications
            .Where(p => p.Language.Code == languageCode)
            .Select(p => p.Code)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        var apiUrls = await GetAllBaseUrls(db);
        var apiUrl = apiUrls.FirstOrDefault();
        if (apiUrl == null)
        {
            logger.Warning("No BaseUrl found for video publication. BaseUrls should be seeded first.");
            return;
        }

        var category = await GetCategory(db, "Dramas");

        foreach (var (key, (publicationName, episodes)) in publications)
        {
            var publicationCode = key.PublicationCode;
            
            if (existingCodes.Contains(publicationCode))
            {
                logger.Information("Skipping video publication {PublicationName} ({PublicationCode}) for language {LanguageCode} - already exists",
                    publicationName, publicationCode, languageCode);
                continue;
            }

            logger.Information("Seeding video publication {PublicationName} ({PublicationCode}) for language {LanguageCode}",
                publicationName, publicationCode, languageCode);

            var publication = new Publication { Code = publicationCode, Name = publicationName };
            var newPublication = await CreateBiblePublication(db, publication, language, category.Id, languageCode, isVideo: true);

            await AddVideoEpisodesToPublicationFromMemory(db, episodes, newPublication, apiUrl);

            await db.BiblePublications.AddAsync(newPublication);
            await db.SaveChangesAsync();
        }
    }

    private async Task AddVideoEpisodesToPublicationFromMemory(
        MediaDbContext db,
        List<VideoEpisode> episodes,
        BiblePublication biblePublication,
        BaseUrl apiUrl)
    {
        foreach (var episode in episodes.OrderBy(e => e.Number))
        {
            var episodeUrlParams = new List<UrlParam>
            {
                new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "pub",
                    Value = biblePublication.Code,
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "track",
                    Value = episode.Number.ToString(),
                    IsQueryParam = true
                },
                new UrlParam
                {
                    BiblePublicationTrackId = 0,
                    Key = "fileformat",
                    Value = "mp4",
                    IsQueryParam = true
                }
            };

            var newTrack = new Shared.Models.Media.BiblePublications.BiblePublicationTrack
            {
                Number = episode.Number,
                Title = episode.Title,
                Publication = biblePublication,
                UrlParams = episodeUrlParams
            };

            biblePublication.Tracks.Add(newTrack);
        }
    }

    #endregion
}
