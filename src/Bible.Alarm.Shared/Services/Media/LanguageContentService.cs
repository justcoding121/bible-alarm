#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for fetching and caching non-English language content on-demand.
/// When a user changes language, this service checks if the content exists in the database,
/// and if not, fetches it from the API and stores it for future use.
/// </summary>
public sealed class LanguageContentService : ILanguageContentService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly HttpClient httpClient;

    public LanguageContentService(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        HttpClient httpClient)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<bool> FetchPublicationTracksAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // "iam" (Kingdom Melodies) doesn't support ad-hoc fetching - it's only seeded once with null language
            if (normalizedPublicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase))
            {
                logger.Warning("Publication {PublicationCode} (Kingdom Melodies) doesn't support ad-hoc fetching", publicationCode);
                return false;
            }

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            // Verify English publication exists
            var englishPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .FirstOrDefaultAsync(
                    bp => bp.Code == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.Code == "E",
                    cancellationToken);

            if (englishPublication == null)
            {
                logger.Warning("English publication {PublicationCode} not found", publicationCode);
                return false;
            }

            // Check if language is available
            var isAvailable = await db.PublicationLanguages
                .Include(pl => pl.Language)
                .AnyAsync(
                    pl => pl.PublicationCode == normalizedPublicationCode &&
                          pl.Language.Code == normalizedLanguageCode,
                    cancellationToken);

            if (!isAvailable)
            {
                logger.Warning("Language {LanguageCode} is not available for publication {PublicationCode}",
                    languageCode, publicationCode);
                return false;
            }

            // Delete existing publication for this language (use case-sensitive code for dramas)
            var existingPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Tracks)
                    .ThenInclude(t => t.UrlParams)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.Code == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.Code == normalizedLanguageCode,
                    cancellationToken);

            if (existingPublication != null)
            {
                logger.Information("Deleting existing publication {PublicationCode} for language {LanguageCode}",
                    publicationCode, languageCode);
                db.BiblePublications.Remove(existingPublication);
                await db.SaveChangesAsync(cancellationToken);
            }

            // Check if this is a drama (uses Mediator API, not GETPUBMEDIALINKS)
            if (isDrama)
            {
                return await FetchDramaPublicationTracksAsync(
                    db, publicationCodeForDb, normalizedLanguageCode, englishPublication, cancellationToken);
            }

            // Determine file format and fetching logic based on category
            // Videos are in "Dramas" category with IsVideo=true, Music is in "Music" category
            var categoryName = englishPublication.Category?.CategoryName ?? "";
            var isVideo = englishPublication.IsVideo;
            var isMusic = categoryName.Equals("Music", StringComparison.OrdinalIgnoreCase);
            var fileFormat = isVideo ? "MP4" : "MP3";
            var trackParam = isVideo ? "&track=" : "";

            var tracks = new List<BiblePublicationTrack>();
            string? localizedPubName = null;
            var trackNumber = 1;
            var consecutiveFailures = 0;
            const int MaxConsecutiveFailures = 3;

            while (consecutiveFailures < MaxConsecutiveFailures)
            {
                try
                {
                    var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&fileformat={fileFormat}&alllangs=0{trackParam}{trackNumber}&langwritten={normalizedLanguageCode}";
                    var response = await httpClient.GetAsync(harvestLink, cancellationToken);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        consecutiveFailures++;
                        trackNumber++;
                        continue;
                    }

                    var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
                    {
                        consecutiveFailures++;
                        trackNumber++;
                        continue;
                    }

                    if (!filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles))
                    {
                        consecutiveFailures++;
                        trackNumber++;
                        continue;
                    }

                    if (!languageFiles.TryGetProperty(fileFormat, out var formatFiles))
                    {
                        consecutiveFailures++;
                        trackNumber++;
                        continue;
                    }

                    // Extract localized publication name (only once)
                    if (localizedPubName == null && root.TryGetProperty("pubName", out var pubNameElement))
                    {
                        var rawName = pubNameElement.GetString();
                        localizedPubName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                    }

                    // Process files
                    if (isVideo)
                    {
                        // For video, find preferred quality (240p) or fallback to first available
                        JsonElement? selectedFile = null;
                        foreach (var file in formatFiles.EnumerateArray())
                        {
                            if (file.TryGetProperty("label", out var labelElement))
                            {
                                var label = labelElement.GetString();
                                if (label == "240p")
                                {
                                    selectedFile = file;
                                    break;
                                }
                            }
                            selectedFile ??= file;
                        }

                        if (selectedFile.HasValue)
                        {
                            var fileElement = selectedFile.Value;
                            if (fileElement.TryGetProperty("file", out var fileInfo) &&
                                fileInfo.TryGetProperty("url", out var urlElement))
                            {
                                var url = urlElement.GetString();
                                if (!string.IsNullOrEmpty(url))
                                {
                                    string title = "Unknown";
                                    if (fileElement.TryGetProperty("title", out var titleElement))
                                    {
                                        var rawTitle = titleElement.GetString();
                                        title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                                    }

                                    double duration = 0;
                                    if (fileElement.TryGetProperty("duration", out var durationElement))
                                    {
                                        duration = durationElement.GetDouble();
                                    }

                            var track = new BiblePublicationTrack
                            {
                                Number = trackNumber,
                                Title = title,
                                UrlParams = new List<UrlParam>
                                        {
                                            new UrlParam
                                            {
                                                Key = "pub",
                                                Value = normalizedPublicationCode,
                                                IsQueryParam = true
                                            },
                                            new UrlParam
                                            {
                                                Key = "track",
                                                Value = trackNumber.ToString(),
                                                IsQueryParam = true
                                            },
                                            new UrlParam
                                            {
                                                Key = "fileformat",
                                                Value = fileFormat.ToLowerInvariant(),
                                                IsQueryParam = true
                                            },
                                            new UrlParam
                                            {
                                                Key = "langwritten",
                                                Value = normalizedLanguageCode,
                                                IsQueryParam = true
                                            }
                                        }
                                    };
                                    tracks.Add(track);
                                    consecutiveFailures = 0;
                                }
                            }
                        }
                    }
                    else
                    {
                        // For music, process all tracks in the response
                        foreach (var musicFile in formatFiles.EnumerateArray())
                        {
                            if (!musicFile.TryGetProperty("file", out var fileElement) ||
                                !fileElement.TryGetProperty("url", out var urlElement))
                            {
                                continue;
                            }

                            var url = urlElement.GetString();
                            if (string.IsNullOrEmpty(url))
                            {
                                continue;
                            }

                            if (!musicFile.TryGetProperty("track", out var trackElement))
                            {
                                continue;
                            }

                            var track = trackElement.GetInt32();
                            if (track == 0 || url.EndsWith(".zip"))
                            {
                                continue;
                            }

                            string title = "Unknown";
                            if (musicFile.TryGetProperty("title", out var titleElement))
                            {
                                var rawTitle = titleElement.GetString();
                                title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                            }

                            if (title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var bibleTrack = new BiblePublicationTrack
                            {
                                Number = trackNumber,
                                Title = title,
                                UrlParams = new List<UrlParam>
                                {
                                    new UrlParam
                                    {
                                        Key = "pub",
                                        Value = normalizedPublicationCode,
                                        IsQueryParam = true
                                    },
                                    new UrlParam
                                    {
                                        Key = "track",
                                        Value = track.ToString(),
                                        IsQueryParam = true
                                    },
                                    new UrlParam
                                    {
                                        Key = "fileformat",
                                        Value = fileFormat.ToLowerInvariant(),
                                        IsQueryParam = true
                                    },
                                    new UrlParam
                                    {
                                        Key = "langwritten",
                                        Value = normalizedLanguageCode,
                                        IsQueryParam = true
                                    }
                                }
                            };
                            tracks.Add(bibleTrack);
                            trackNumber++;
                        }
                        consecutiveFailures = 0;
                        break; // Music returns all tracks in one response
                    }

                    trackNumber++;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
                {
                    consecutiveFailures++;
                    trackNumber++;
                    continue;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to fetch track {TrackNumber} for publication {PublicationCode} in language {LanguageCode}",
                        trackNumber, publicationCode, languageCode);
                    consecutiveFailures++;
                    trackNumber++;
                    continue;
                }
            }

            if (tracks.Count == 0)
            {
                logger.Warning("No tracks found for publication {PublicationCode} in language {LanguageCode}",
                    publicationCode, languageCode);
                return false;
            }

            // Get or create language and category
            var language = await db.Languages
                .FirstOrDefaultAsync(l => l.Code == normalizedLanguageCode, cancellationToken);
            
            if (language == null)
            {
                logger.Warning("Language {LanguageCode} not found in database", languageCode);
                return false;
            }

            var category = englishPublication.Category;
            if (category == null)
            {
                logger.Warning("Category not found for English publication {PublicationCode}", publicationCode);
                return false;
            }

            // Get BaseUrl for tracks
            var baseUrl = await db.BaseUrls
                .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
                .FirstOrDefaultAsync(cancellationToken);

            if (baseUrl == null)
            {
                logger.Warning("No BaseUrl found for publication tracks");
                return false;
            }

            // Create publication
            var publicationName = localizedPubName ?? englishPublication.Name;
            var publication = new BiblePublication
            {
                Code = normalizedPublicationCode,
                Name = publicationName,
                Language = language,
                Category = category,
                CategoryId = category.Id,
                LanguageId = language.Id,
                IsVideo = isVideo,
                Tracks = tracks,
                Sections = new List<BiblePublicationSection>()
            };

            // Set publication reference on tracks
            foreach (var track in tracks)
            {
                track.Publication = publication;
            }

            // Add BaseUrl reference to tracks via UrlParams
            foreach (var track in tracks)
            {
                foreach (var urlParam in track.UrlParams)
                {
                    urlParam.BaseUrl = baseUrl;
                    urlParam.BaseUrlId = baseUrl.Id;
                }
            }

            db.BiblePublications.Add(publication);
            await db.SaveChangesAsync(cancellationToken);

            logger.Information("Successfully fetched {Count} tracks for publication {PublicationCode} in language {LanguageCode}",
                tracks.Count, publicationCode, languageCode);

            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching publication tracks for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }

    public async Task<bool> FetchPublicationSectionsAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // "iam" (Kingdom Melodies) doesn't support ad-hoc fetching - it's only seeded once with null language
            if (normalizedPublicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase))
            {
                logger.Warning("Publication {PublicationCode} (Kingdom Melodies) doesn't support ad-hoc fetching", publicationCode);
                return false;
            }

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            // Verify English publication exists
            var englishPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .Include(bp => bp.Sections)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.Code == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.Code == "E",
                    cancellationToken);

            if (englishPublication == null)
            {
                logger.Warning("English publication {PublicationCode} not found", publicationCode);
                return false;
            }

            // Check if language is available
            var isAvailable = await db.PublicationLanguages
                .Include(pl => pl.Language)
                .AnyAsync(
                    pl => pl.PublicationCode == normalizedPublicationCode &&
                          pl.Language.Code == normalizedLanguageCode,
                    cancellationToken);

            if (!isAvailable)
            {
                logger.Warning("Language {LanguageCode} is not available for publication {PublicationCode}",
                    languageCode, publicationCode);
                return false;
            }

            // Get all section codes from SectionLanguages for this publication+language
            var sectionCodes = await db.SectionLanguages
                .Include(sl => sl.Language)
                .Where(sl => sl.PublicationCode == normalizedPublicationCode &&
                           sl.Language.Code == normalizedLanguageCode)
                .Select(sl => sl.SectionCode)
                .Distinct()
                .OrderBy(sc => sc)
                .ToListAsync(cancellationToken);

            if (sectionCodes.Count == 0)
            {
                logger.Warning("No sections found for publication {PublicationCode} in language {LanguageCode}",
                    publicationCode, languageCode);
                return false;
            }

            // Delete existing publication for this language (including sections and tracks)
            // Use case-sensitive code for dramas
            var existingPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Sections)
                    .ThenInclude(s => s.Tracks)
                        .ThenInclude(t => t.UrlParams)
                .Include(bp => bp.Sections)
                    .ThenInclude(s => s.UrlParams)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.Code == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.Code == normalizedLanguageCode,
                    cancellationToken);

            if (existingPublication != null)
            {
                logger.Information("Deleting existing publication {PublicationCode} for language {LanguageCode}",
                    publicationCode, languageCode);
                db.BiblePublications.Remove(existingPublication);
                await db.SaveChangesAsync(cancellationToken);
            }

            // Get or create language and category
            var language = await db.Languages
                .FirstOrDefaultAsync(l => l.Code == normalizedLanguageCode, cancellationToken);
            
            if (language == null)
            {
                logger.Warning("Language {LanguageCode} not found in database", languageCode);
                return false;
            }

            var category = englishPublication.Category;
            if (category == null)
            {
                logger.Warning("Category not found for English publication {PublicationCode}", publicationCode);
                return false;
            }

            // Get BaseUrl
            var baseUrl = await db.BaseUrls
                .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
                .FirstOrDefaultAsync(cancellationToken);

            if (baseUrl == null)
            {
                logger.Warning("No BaseUrl found");
                return false;
            }

            // Determine if this is a Bible publication (uses booknum) or Drama (uses pub=sectionCode)
            var isBible = category.CategoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase);
            var sections = new List<BiblePublicationSection>();
            string? localizedPubName = null;

            foreach (var sectionCode in sectionCodes)
            {
                try
                {
                    // For Bible, use booknum parameter; for Drama, use pub=sectionCode
                    var harvestLink = isBible
                        ? $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&booknum={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}"
                        : $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";

                    var response = await httpClient.GetAsync(harvestLink, cancellationToken);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.Debug("Section {SectionCode} not available for publication {PublicationCode} in language {LanguageCode}",
                            sectionCode, publicationCode, languageCode);
                        continue;
                    }

                    var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
                    {
                        continue;
                    }

                    // Extract section name from API response
                    // The pubName field should contain the localized section name when langwritten is set correctly
                    string? sectionName = null;
                    if (root.TryGetProperty("pubName", out var pubNameElement))
                    {
                        var rawName = pubNameElement.GetString();
                        sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                    }
                    
                    // If pubName is not found, log a warning (this might indicate the API response structure is different)
                    if (sectionName == null)
                    {
                        logger.Debug("Section name (pubName) not found in API response for section {SectionCode} in language {LanguageCode}",
                            sectionCode, normalizedLanguageCode);
                    }

                    // Extract localized publication name (only once)
                    if (localizedPubName == null && root.TryGetProperty("parentPubName", out var parentPubNameElement))
                    {
                        var rawName = parentPubNameElement.GetString();
                        localizedPubName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                    }

                    // Create section (without tracks - those are fetched separately)
                    var section = new BiblePublicationSection
                    {
                        Name = sectionName ?? sectionCode,
                        SectionCode = sectionCode.ToLowerInvariant(), // Use section code from API
                        UrlParams = new List<UrlParam>(),
                        Tracks = new List<BiblePublicationTrack>()
                    };

                    // Add URL params based on type
                    if (isBible)
                    {
                        section.UrlParams.Add(new UrlParam
                        {
                            Key = "pub",
                            Value = normalizedPublicationCode,
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        });
                        section.UrlParams.Add(new UrlParam
                        {
                            Key = "booknum",
                            Value = sectionCode,
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        });
                    }
                    else
                    {
                        section.UrlParams.Add(new UrlParam
                        {
                            Key = "pub",
                            Value = sectionCode,
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        });
                    }

                    section.UrlParams.Add(new UrlParam
                    {
                        Key = "fileformat",
                        Value = "mp3",
                        IsQueryParam = true,
                        BaseUrl = baseUrl,
                        BaseUrlId = baseUrl.Id
                    });

                    sections.Add(section);
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
                {
                    logger.Debug("Section {SectionCode} not available for publication {PublicationCode} in language {LanguageCode}",
                        sectionCode, publicationCode, languageCode);
                    continue;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to fetch section {SectionCode} for publication {PublicationCode} in language {LanguageCode}",
                        sectionCode, publicationCode, languageCode);
                    continue;
                }
            }

            if (sections.Count == 0)
            {
                logger.Warning("No sections found for publication {PublicationCode} in language {LanguageCode}",
                    publicationCode, languageCode);
                return false;
            }

            // Create publication
            var publicationName = localizedPubName ?? englishPublication.Name;
            var publication = new BiblePublication
            {
                Code = normalizedPublicationCode,
                Name = publicationName,
                Language = language,
                Category = category,
                CategoryId = category.Id,
                LanguageId = language.Id,
                IsVideo = false,
                Tracks = new List<BiblePublicationTrack>(),
                Sections = sections
            };

            // Set publication reference on sections
            // EF Core will automatically set BiblePublicationId when we save
            foreach (var section in sections)
            {
                section.BiblePublication = publication;
            }

            db.BiblePublications.Add(publication);
            await db.SaveChangesAsync(cancellationToken);

            logger.Information("Successfully fetched {Count} sections for publication {PublicationCode} in language {LanguageCode}",
                sections.Count, publicationCode, languageCode);

            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching publication sections for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }

    public async Task<bool> FetchSectionTracksAsync(
        string publicationCode,
        string sectionCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // Get the publication for this language
            var publication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .Include(bp => bp.Sections)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.Code == normalizedPublicationCode &&
                          bp.Language != null &&
                          bp.Language.Code == normalizedLanguageCode,
                    cancellationToken);

            if (publication == null)
            {
                logger.Warning("Publication {PublicationCode} for language {LanguageCode} not found. Fetch sections first.",
                    publicationCode, languageCode);
                return false;
            }

            // Find the section - reload from database to ensure we have the correct IDs
            var section = await db.BiblePublicationSections
                .Include(s => s.UrlParams)
                .FirstOrDefaultAsync(
                    s => s.BiblePublicationId == publication.Id && s.SectionCode == normalizedSectionCode,
                    cancellationToken);

            if (section == null)
            {
                // Try to find by UrlParam as fallback - need to check sections that belong to this publication
                var allSections = await db.BiblePublicationSections
                    .Include(s => s.UrlParams)
                    .Where(s => s.BiblePublicationId == publication.Id)
                    .ToListAsync(cancellationToken);
                
                section = allSections.FirstOrDefault(s =>
                    s.UrlParams.Any(up => up.Key.Equals("booknum", StringComparison.OrdinalIgnoreCase) &&
                                         up.Value == normalizedSectionCode) ||
                    s.UrlParams.Any(up => up.Key.Equals("pub", StringComparison.OrdinalIgnoreCase) &&
                                         up.Value.Equals(normalizedSectionCode, StringComparison.OrdinalIgnoreCase)));
            }

            if (section == null)
            {
                logger.Warning("Section {SectionCode} not found in publication {PublicationCode} for language {LanguageCode}",
                    sectionCode, publicationCode, languageCode);
                return false;
            }

            // Check if language is available for this section
            var isAvailable = await db.SectionLanguages
                .Include(sl => sl.Language)
                .AnyAsync(
                    sl => sl.PublicationCode == normalizedPublicationCode &&
                          sl.SectionCode == normalizedSectionCode &&
                          sl.Language.Code == normalizedLanguageCode,
                    cancellationToken);

            if (!isAvailable)
            {
                logger.Warning("Language {LanguageCode} is not available for section {SectionCode} of publication {PublicationCode}",
                    languageCode, sectionCode, publicationCode);
                return false;
            }

            // Delete existing tracks for this section
            if (section.Tracks.Count > 0)
            {
                logger.Information("Deleting {Count} existing tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                    section.Tracks.Count, sectionCode, publicationCode, languageCode);
                db.BiblePublicationTracks.RemoveRange(section.Tracks);
                await db.SaveChangesAsync(cancellationToken);
                section.Tracks.Clear();
            }

            // Determine fetching logic based on category
            // Bible uses booknum parameter, Drama uses pub=sectionCode
            var categoryName = publication.Category?.CategoryName ?? "";
            var isBible = categoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase);

            // Build harvest link
            var harvestLink = isBible
                ? $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&booknum={normalizedSectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}"
                : $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedSectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";

            var response = await httpClient.GetAsync(harvestLink, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                logger.Warning("Failed to fetch tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                    sectionCode, publicationCode, languageCode);
                return false;
            }

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
            {
                logger.Warning("Invalid response format for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                    sectionCode, publicationCode, languageCode);
                return false;
            }

            if (!filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
                !languageFiles.TryGetProperty("MP3", out var mp3Files))
            {
                logger.Warning("No MP3 files found for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                    sectionCode, publicationCode, languageCode);
                return false;
            }

            // Get BaseUrl
            var baseUrl = await db.BaseUrls
                .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
                .FirstOrDefaultAsync(cancellationToken);

            if (baseUrl == null)
            {
                logger.Warning("No BaseUrl found");
                return false;
            }

            var tracks = new List<BiblePublicationTrack>();
            var trackNumber = 1;

            foreach (var trackFile in mp3Files.EnumerateArray())
            {
                if (!trackFile.TryGetProperty("file", out var fileElement))
                {
                    continue;
                }

                string? url = null;
                if (fileElement.ValueKind == JsonValueKind.String)
                {
                    url = fileElement.GetString();
                }
                else if (fileElement.ValueKind == JsonValueKind.Object && fileElement.TryGetProperty("url", out var urlElement))
                {
                    url = urlElement.GetString();
                }

                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                string title = "Unknown";
                if (trackFile.TryGetProperty("title", out var titleElement))
                {
                    if (titleElement.ValueKind == JsonValueKind.String)
                    {
                        var rawTitle = titleElement.GetString();
                        title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                    }
                    else if (titleElement.ValueKind == JsonValueKind.Object && titleElement.TryGetProperty("text", out var titleTextElement))
                    {
                        var rawTitle = titleTextElement.GetString();
                        title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                    }
                    
                    // For Bible tracks, remove book name prefix (e.g., "ഉൽപത്തി - അധ്യായം 1" -> "അധ്യായം 1")
                    // The API returns titles like "{book name} - {chapter name}" or "{book name} - Chapter {number}"
                    if (isBible && !string.IsNullOrEmpty(title) && title != "Unknown")
                    {
                        // Split by common separators: " - ", " – ", " — ", " -", "- "
                        var separators = new[] { " - ", " – ", " — ", " -", "- " };
                        foreach (var separator in separators)
                        {
                            if (title.Contains(separator))
                            {
                                var parts = title.Split(new[] { separator }, StringSplitOptions.None);
                                if (parts.Length > 1)
                                {
                                    // Take the last part (chapter name)
                                    title = parts[parts.Length - 1].Trim();
                                    break;
                                }
                            }
                        }
                    }
                }

                var track = new BiblePublicationTrack
                {
                    Number = trackNumber,
                    Title = title,
                    Publication = publication,
                    BiblePublicationId = publication.Id,
                    Section = section,
                    BiblePublicationSectionId = section.Id,
                    UrlParams = new List<UrlParam>()
                };

                // Add URL params based on type
                if (isBible)
                {
                    track.UrlParams.Add(new UrlParam
                    {
                        Key = "pub",
                        Value = normalizedPublicationCode,
                        IsQueryParam = true,
                        BaseUrl = baseUrl,
                        BaseUrlId = baseUrl.Id
                    });
                    track.UrlParams.Add(new UrlParam
                    {
                        Key = "booknum",
                        Value = normalizedSectionCode,
                        IsQueryParam = true,
                        BaseUrl = baseUrl,
                        BaseUrlId = baseUrl.Id
                    });
                }
                else
                {
                    track.UrlParams.Add(new UrlParam
                    {
                        Key = "pub",
                        Value = normalizedSectionCode,
                        IsQueryParam = true,
                        BaseUrl = baseUrl,
                        BaseUrlId = baseUrl.Id
                    });
                }

                track.UrlParams.Add(new UrlParam
                {
                    Key = "track",
                    Value = trackNumber.ToString(),
                    IsQueryParam = true,
                    BaseUrl = baseUrl,
                    BaseUrlId = baseUrl.Id
                });

                track.UrlParams.Add(new UrlParam
                {
                    Key = "fileformat",
                    Value = "mp3",
                    IsQueryParam = true,
                    BaseUrl = baseUrl,
                    BaseUrlId = baseUrl.Id
                });

                track.UrlParams.Add(new UrlParam
                {
                    Key = "alllangs",
                    Value = "0",
                    IsQueryParam = true,
                    BaseUrl = baseUrl,
                    BaseUrlId = baseUrl.Id
                });

                track.UrlParams.Add(new UrlParam
                {
                    Key = "langwritten",
                    Value = normalizedLanguageCode,
                    IsQueryParam = true,
                    BaseUrl = baseUrl,
                    BaseUrlId = baseUrl.Id
                });

                tracks.Add(track);
                trackNumber++;
            }

            if (tracks.Count == 0)
            {
                logger.Warning("No tracks found for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                    sectionCode, publicationCode, languageCode);
                return false;
            }

            // Add tracks to section
            section.Tracks = tracks;
            await db.SaveChangesAsync(cancellationToken);

            logger.Information("Successfully fetched {Count} tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                tracks.Count, sectionCode, publicationCode, languageCode);

            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching section tracks for {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                sectionCode, publicationCode, languageCode);
            return false;
        }
    }

    /// <summary>
    /// Fetches and creates English publication from scratch (for initial seeding).
    /// Determines category from publication code and uses discovered languages table.
    /// </summary>
    public async Task<bool> SeedEnglishPublicationAsync(
        string publicationCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            const string EnglishCode = "E";
            var normalizedLanguageCode = EnglishCode.ToUpperInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            // For "iam" (Kingdom Melodies), language should be null
            var isIam = normalizedPublicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase);
            
            Language? language = null;
            if (!isIam)
            {
                // Get or create English language (only for non-iam publications)
                language = await db.Languages
                    .FirstOrDefaultAsync(l => l.Code == normalizedLanguageCode, cancellationToken);
                
                if (language == null)
                {
                    logger.Warning("English language (E) not found in database");
                    return false;
                }
            }

            // Determine category from publication code using centralized mapping
            var categoryName = JwSourceHelper.GetCategoryName(publicationCode);
            if (categoryName == null)
            {
                logger.Warning("Unknown publication type for {PublicationCode}", publicationCode);
                return false;
            }

            // Determine if this is a video (videos are in Dramas category but have IsVideo=true)
            var isVideo = JwSourceHelper.VideoPublicationCodes.Contains(normalizedPublicationCode);

            // Get category from database
            var category = await db.Categories
                .FirstOrDefaultAsync(c => c.CategoryName == categoryName, cancellationToken);
            
            if (category == null)
            {
                logger.Warning("Category {CategoryName} not found in database", categoryName);
                return false;
            }

            // Check if publication already exists
            // For "iam", check for null language; for others, check for English language
            // Use case-sensitive code for dramas
            var existingPublication = isIam
                ? await db.BiblePublications
                    .Include(bp => bp.Language)
                    .FirstOrDefaultAsync(
                        bp => bp.Code == publicationCodeForDb &&
                              bp.Language == null,
                        cancellationToken)
                : await db.BiblePublications
                    .Include(bp => bp.Language)
                    .FirstOrDefaultAsync(
                        bp => bp.Code == publicationCodeForDb &&
                              bp.Language != null &&
                              bp.Language.Code == normalizedLanguageCode,
                        cancellationToken);

            if (existingPublication != null)
            {
                logger.Information("English publication {PublicationCode} already exists, skipping", publicationCode);
                return true;
            }

            // Determine if this is a sectioned publication (Bible or iam) or flat (Drama/Music/Video)
            // Sections are only used by: Bible (books 1-66) and Music "iam" (Kingdom Melodies discs)
            var hasSections = PublicationTypeHelper.HasSectionStructure(publicationCode);

            if (hasSections)
            {
                List<string> sectionCodes;
                
                if (normalizedPublicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase))
                {
                    // For "iam" (Kingdom Melodies), use hardcoded disc codes (iam-1 to iam-9)
                    // Note: iam doesn't have language discovery, so SectionLanguages won't have entries
                    sectionCodes = Enumerable.Range(1, 9).Select(i => $"iam-{i}").ToList();
                }
                else
                {
                    // For Bible publications, use hardcoded book numbers 1-66
                    sectionCodes = Enumerable.Range(1, 66).Select(i => i.ToString()).ToList();
                }

                // Use the existing FetchPublicationSectionsAsync logic but adapted for English
                // For "iam", language will be null
                return await FetchEnglishPublicationSectionsAsync(
                    db, publicationCodeForDb, normalizedLanguageCode, language, category, 
                    categoryName, isVideo, sectionCodes, cancellationToken);
            }
            else
            {
                // Use the existing FetchPublicationTracksAsync logic but adapted for English
                // Note: language is never null here since "iam" has sections (handled above)
                if (language == null)
                {
                    logger.Warning("Language is null for publication {PublicationCode} which should have flat tracks", publicationCode);
                    return false;
                }
                
                return await FetchEnglishPublicationTracksAsync(
                    db, publicationCodeForDb, normalizedLanguageCode, language, category, 
                    categoryName, isVideo, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error seeding English publication {PublicationCode}", publicationCode);
            return false;
        }
    }

    private async Task<bool> FetchEnglishPublicationSectionsAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        Language? language,
        Category category,
        string categoryName,
        bool isVideo,
        List<string> sectionCodes,
        CancellationToken cancellationToken)
    {
        // Get BaseUrl
        var baseUrl = await db.BaseUrls
            .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .FirstOrDefaultAsync(cancellationToken);

        if (baseUrl == null)
        {
            logger.Warning("No BaseUrl found");
            return false;
        }

        // Determine if this is a Bible publication (uses booknum) or iam (Kingdom Melodies)
        var isBible = categoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase);
        var isIamPublication = normalizedPublicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase);
        var sections = new List<BiblePublicationSection>();
        string? localizedPubName = null;

        foreach (var sectionCode in sectionCodes)
        {
            try
            {
                // For Bible, use booknum parameter; for iam (Kingdom Melodies), use pub=sectionCode with langwritten=E
                // Note: iam uses langwritten=E even though it has no language (melody music)
                var harvestLink = isBible
                    ? $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&booknum={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}"
                    : isIamPublication
                        ? $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten=E"
                        : $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";

                var response = await httpClient.GetAsync(harvestLink, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    logger.Debug("Section {SectionCode} not available for publication {PublicationCode} in English",
                        sectionCode, normalizedPublicationCode);
                    continue;
                }

                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
                {
                    continue;
                }

                // Extract section name
                string? sectionName = null;
                if (root.TryGetProperty("pubName", out var pubNameElement))
                {
                    var rawName = pubNameElement.GetString();
                    sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                }

                // For iam, always use "Kingdom Melodies" as publication name (disc names like "Kingdom Melodies, Volume 1" are section names)
                if (isIamPublication)
                {
                    // Use hardcoded publication name for Kingdom Melodies
                    localizedPubName = "Kingdom Melodies";
                }
                // Extract localized publication name (only once) - but skip for iam since we already set it above
                else if (localizedPubName == null && root.TryGetProperty("parentPubName", out var parentPubNameElement))
                {
                    var rawName = parentPubNameElement.GetString();
                    localizedPubName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                }

                // For Drama, also check category.name
                if (localizedPubName == null && !isBible && !isIamPublication && root.TryGetProperty("category", out var categoryElement) &&
                    categoryElement.TryGetProperty("name", out var categoryNameElement))
                {
                    var rawName = categoryNameElement.GetString();
                    localizedPubName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                }

                // Create section
                var section = new BiblePublicationSection
                {
                    Name = sectionName ?? sectionCode,
                    SectionCode = sectionCode.ToLowerInvariant(),
                    UrlParams = new List<UrlParam>(),
                    Tracks = new List<BiblePublicationTrack>()
                };

                // For "iam", fetch tracks from the API response (all tracks are in one call)
                if (isIamPublication)
                {
                    // Parse tracks from files.E.MP3
                    if (filesElement.TryGetProperty("E", out var englishFiles) &&
                        englishFiles.TryGetProperty("MP3", out var mp3Files))
                    {
                        var trackNumber = 1;
                        foreach (var trackFile in mp3Files.EnumerateArray())
                        {
                            if (!trackFile.TryGetProperty("file", out var fileElement) ||
                                !fileElement.TryGetProperty("url", out var urlElement))
                            {
                                continue;
                            }

                            var url = urlElement.GetString();
                            if (string.IsNullOrEmpty(url))
                            {
                                continue;
                            }

                            // Get track number from API (original track number within the disc)
                            int originalTrackNumber = 0;
                            if (trackFile.TryGetProperty("track", out var trackElement))
                            {
                                originalTrackNumber = trackElement.GetInt32();
                            }

                            if (originalTrackNumber == 0)
                            {
                                continue;
                            }

                            // Get title
                            string title = "Unknown";
                            if (trackFile.TryGetProperty("title", out var titleElement))
                            {
                                var rawTitle = titleElement.GetString();
                                title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                            }

                            // Skip audio descriptions
                            if (title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            // Create track with URL params
                            var trackUrlParams = new List<UrlParam>
                            {
                                new UrlParam
                                {
                                    Key = "pub",
                                    Value = sectionCode,
                                    IsQueryParam = true,
                                    BaseUrl = baseUrl,
                                    BaseUrlId = baseUrl.Id
                                },
                                new UrlParam
                                {
                                    Key = "fileformat",
                                    Value = "mp3",
                                    IsQueryParam = true,
                                    BaseUrl = baseUrl,
                                    BaseUrlId = baseUrl.Id
                                },
                                new UrlParam
                                {
                                    Key = "track",
                                    Value = originalTrackNumber.ToString(),
                                    IsQueryParam = true,
                                    BaseUrl = baseUrl,
                                    BaseUrlId = baseUrl.Id
                                }
                            };

                            var track = new BiblePublicationTrack
                            {
                                Number = trackNumber,
                                Title = title,
                                UrlParams = trackUrlParams
                            };

                            section.Tracks.Add(track);
                            trackNumber++;
                        }
                    }
                }

                // Add URL params based on type
                if (isBible)
                {
                    section.UrlParams.Add(new UrlParam
                    {
                        Key = "pub",
                        Value = normalizedPublicationCode,
                        IsQueryParam = true,
                        BaseUrl = baseUrl,
                        BaseUrlId = baseUrl.Id
                    });
                    section.UrlParams.Add(new UrlParam
                    {
                        Key = "booknum",
                        Value = sectionCode,
                        IsQueryParam = true,
                        BaseUrl = baseUrl,
                        BaseUrlId = baseUrl.Id
                    });
                }
                else
                {
                    section.UrlParams.Add(new UrlParam
                    {
                        Key = "pub",
                        Value = sectionCode,
                        IsQueryParam = true,
                        BaseUrl = baseUrl,
                        BaseUrlId = baseUrl.Id
                    });
                }

                section.UrlParams.Add(new UrlParam
                {
                    Key = "fileformat",
                    Value = "mp3",
                    IsQueryParam = true,
                    BaseUrl = baseUrl,
                    BaseUrlId = baseUrl.Id
                });

                sections.Add(section);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                logger.Debug("Section {SectionCode} not available for publication {PublicationCode} in English",
                    sectionCode, normalizedPublicationCode);
                continue;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to fetch section {SectionCode} for publication {PublicationCode} in English",
                    sectionCode, normalizedPublicationCode);
                continue;
            }
        }

        if (sections.Count == 0)
        {
            logger.Warning("No sections found for publication {PublicationCode} in English", normalizedPublicationCode);
            return false;
        }

        // Create publication
        var publicationName = localizedPubName ?? normalizedPublicationCode;
        var publication = new BiblePublication
        {
            Code = normalizedPublicationCode,
            Name = publicationName,
            Language = language, // null for "iam"
            Category = category,
            CategoryId = category.Id,
            LanguageId = language?.Id, // null for "iam"
            IsVideo = isVideo,
            Tracks = new List<BiblePublicationTrack>(),
            Sections = sections
        };

        // Set publication reference on sections and tracks
        foreach (var section in sections)
        {
            section.BiblePublication = publication;
            
            // Set publication reference on tracks (for iam, tracks are already in sections)
            foreach (var track in section.Tracks)
            {
                track.Publication = publication;
                track.Section = section;
            }
        }

        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully seeded {Count} sections for English publication {PublicationCode}",
            sections.Count, normalizedPublicationCode);

        return true;
    }

    private async Task<bool> FetchEnglishPublicationTracksAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        Language language,
        Category category,
        string categoryName,
        bool isVideo,
        CancellationToken cancellationToken)
    {
        // Check if this is a drama (uses Mediator API, not GETPUBMEDIALINKS)
        var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
        
        if (isDrama)
        {
            return await FetchEnglishDramaPublicationAsync(
                db, normalizedPublicationCode, normalizedLanguageCode, language, category, cancellationToken);
        }

        // Determine file format and fetching logic based on category
        var isMusic = categoryName.Equals("Music", StringComparison.OrdinalIgnoreCase);
        var fileFormat = isVideo ? "MP4" : "MP3";
        var trackParam = isVideo ? "&track=" : "";

        var tracks = new List<BiblePublicationTrack>();
        string? localizedPubName = null;
        var trackNumber = 1;
        var consecutiveFailures = 0;
        const int MaxConsecutiveFailures = 3;

        while (consecutiveFailures < MaxConsecutiveFailures)
        {
            try
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&fileformat={fileFormat}&alllangs=0{trackParam}{trackNumber}&langwritten={normalizedLanguageCode}";
                var response = await httpClient.GetAsync(harvestLink, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    consecutiveFailures++;
                    trackNumber++;
                    continue;
                }

                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
                {
                    consecutiveFailures++;
                    trackNumber++;
                    continue;
                }

                // Extract localized publication name (only once)
                if (localizedPubName == null)
                {
                    if (root.TryGetProperty("pubName", out var pubNameElement))
                    {
                        var rawName = pubNameElement.GetString();
                        localizedPubName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                    }
                }

                if (!filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
                    !languageFiles.TryGetProperty(fileFormat, out var formatFiles))
                {
                    consecutiveFailures++;
                    trackNumber++;
                    continue;
                }

                foreach (var trackFile in formatFiles.EnumerateArray())
                {
                    if (!trackFile.TryGetProperty("file", out var fileElement))
                    {
                        continue;
                    }

                    string? url = null;
                    if (fileElement.ValueKind == JsonValueKind.String)
                    {
                        url = fileElement.GetString();
                    }
                    else if (fileElement.ValueKind == JsonValueKind.Object && fileElement.TryGetProperty("url", out var urlElement))
                    {
                        url = urlElement.GetString();
                    }

                    if (string.IsNullOrEmpty(url))
                    {
                        continue;
                    }

                    string title = "Unknown";
                    if (trackFile.TryGetProperty("title", out var titleElement))
                    {
                        if (titleElement.ValueKind == JsonValueKind.String)
                        {
                            var rawTitle = titleElement.GetString();
                            title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                        }
                        else if (titleElement.ValueKind == JsonValueKind.Object && titleElement.TryGetProperty("text", out var titleTextElement))
                        {
                            var rawTitle = titleTextElement.GetString();
                            title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                        }
                    }

                    // Get BaseUrl
                    var baseUrl = await db.BaseUrls
                        .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
                        .FirstOrDefaultAsync(cancellationToken);

                    if (baseUrl == null)
                    {
                        logger.Warning("No BaseUrl found for publication tracks");
                        return false;
                    }

                    var trackUrlParams = new List<UrlParam>
                    {
                        new UrlParam
                        {
                            Key = "pub",
                            Value = normalizedPublicationCode,
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "track",
                            Value = trackNumber.ToString(),
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "fileformat",
                            Value = fileFormat.ToLowerInvariant(),
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        }
                    };

                    tracks.Add(new BiblePublicationTrack
                    {
                        Number = trackNumber,
                        Title = title,
                        UrlParams = trackUrlParams
                    });

                    trackNumber++;
                }

                consecutiveFailures = 0;
                if (isMusic)
                {
                    break; // Music returns all tracks in one response
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                consecutiveFailures++;
                trackNumber++;
                continue;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to fetch track {TrackNumber} for publication {PublicationCode} in English",
                    trackNumber, normalizedPublicationCode);
                consecutiveFailures++;
                trackNumber++;
                continue;
            }
        }

        if (tracks.Count == 0)
        {
            logger.Warning("No tracks found for publication {PublicationCode} in English", normalizedPublicationCode);
            return false;
        }

        // Get BaseUrl for tracks
        var finalBaseUrl = await db.BaseUrls
            .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .FirstOrDefaultAsync(cancellationToken);

        if (finalBaseUrl == null)
        {
            logger.Warning("No BaseUrl found for publication tracks");
            return false;
        }

        // Create publication
        var publicationName = localizedPubName ?? normalizedPublicationCode;
        var publication = new BiblePublication
        {
            Code = normalizedPublicationCode,
            Name = publicationName,
            Language = language,
            Category = category,
            CategoryId = category.Id,
            LanguageId = language.Id,
            IsVideo = isVideo,
            Tracks = tracks,
            Sections = new List<BiblePublicationSection>()
        };

        // Set publication reference on tracks
        foreach (var track in tracks)
        {
            track.Publication = publication;
        }

        // Add BaseUrl reference to tracks via UrlParams
        foreach (var track in tracks)
        {
            foreach (var urlParam in track.UrlParams)
            {
                urlParam.BaseUrl = finalBaseUrl;
                urlParam.BaseUrlId = finalBaseUrl.Id;
            }
        }

        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully seeded {Count} tracks for English publication {PublicationCode}",
            tracks.Count, normalizedPublicationCode);

        return true;
    }

    private async Task<bool> FetchEnglishDramaPublicationAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        Language language,
        Category category,
        CancellationToken cancellationToken)
    {
        // Get BaseUrl
        var baseUrl = await db.BaseUrls
            .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .FirstOrDefaultAsync(cancellationToken);

        if (baseUrl == null)
        {
            logger.Warning("No BaseUrl found");
            return false;
        }

        // Use Mediator API to get the category (case-sensitive: "Dramas" or "DramaticBibleReadings")
        // Note: normalizedPublicationCode is lowercase, but API requires exact case
        string categoryKey;
        if (normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase))
        {
            categoryKey = "Dramas";
        }
        else if (normalizedPublicationCode.Equals("dramaticbiblereadings", StringComparison.OrdinalIgnoreCase))
        {
            categoryKey = "DramaticBibleReadings";
        }
        else
        {
            logger.Warning("Unknown drama publication code: {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        var mediatorUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{normalizedLanguageCode}/{categoryKey}?detailed=1";
        
        var response = await httpClient.GetAsync(mediatorUrl, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            logger.Warning("Failed to fetch drama category {CategoryKey} for language {LanguageCode}: {StatusCode}",
                categoryKey, normalizedLanguageCode, response.StatusCode);
            return false;
        }

        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("category", out var categoryElement))
        {
            logger.Warning("Invalid response structure for drama category {CategoryKey}", categoryKey);
            return false;
        }

        // Extract localized publication name
        string? localizedPubName = null;
        if (categoryElement.TryGetProperty("name", out var nameElement))
        {
            var rawName = nameElement.GetString();
            localizedPubName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
        }

        // Extract section codes from category.media array
        if (!categoryElement.TryGetProperty("media", out var mediaArray) || mediaArray.ValueKind != JsonValueKind.Array)
        {
            logger.Warning("No media items found in drama category {CategoryKey}", categoryKey);
            return false;
        }

        var sectionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mediaItem in mediaArray.EnumerateArray())
        {
            // Extract section code from naturalKey: "pub-{sectionCode}_{lang}_{number}_AUDIO"
            if (mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
            {
                var naturalKey = naturalKeyElement.GetString() ?? "";
                if (naturalKey.StartsWith("pub-", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = naturalKey.Split('_');
                    if (parts.Length > 0)
                    {
                        var sectionCode = parts[0].Substring(4); // Remove "pub-" prefix
                        if (!string.IsNullOrEmpty(sectionCode))
                        {
                            sectionCodes.Add(sectionCode);
                        }
                    }
                }
            }
        }

        if (sectionCodes.Count == 0)
        {
            logger.Warning("No section codes found in drama category {CategoryKey}", categoryKey);
            return false;
        }

        // Fetch tracks for each section using GETPUBMEDIALINKS
        var allTracks = new List<BiblePublicationTrack>();
        var trackNumber = 1;

        foreach (var sectionCode in sectionCodes.OrderBy(sc => sc))
        {
            try
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";
                var sectionResponse = await httpClient.GetAsync(harvestLink, cancellationToken);
                
                if (!sectionResponse.IsSuccessStatusCode)
                {
                    logger.Debug("Section {SectionCode} not available for drama {PublicationCode} in English",
                        sectionCode, normalizedPublicationCode);
                    continue;
                }

                var sectionJsonString = await sectionResponse.Content.ReadAsStringAsync(cancellationToken);
                using var sectionDoc = JsonDocument.Parse(sectionJsonString);
                var sectionRoot = sectionDoc.RootElement;

                if (sectionRoot.ValueKind != JsonValueKind.Object || !sectionRoot.TryGetProperty("files", out var sectionFilesElement))
                {
                    continue;
                }

                if (!sectionFilesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
                    !languageFiles.TryGetProperty("MP3", out var mp3Files))
                {
                    continue;
                }

                foreach (var trackFile in mp3Files.EnumerateArray())
                {
                    if (!trackFile.TryGetProperty("file", out var fileElement) ||
                        !fileElement.TryGetProperty("url", out var urlElement))
                    {
                        continue;
                    }

                    var url = urlElement.GetString();
                    if (string.IsNullOrEmpty(url))
                    {
                        continue;
                    }

                    // Get track number from API
                    int originalTrackNumber = 0;
                    if (trackFile.TryGetProperty("track", out var trackElement))
                    {
                        originalTrackNumber = trackElement.GetInt32();
                    }

                    if (originalTrackNumber == 0)
                    {
                        continue;
                    }

                    // Get title
                    string title = "Unknown";
                    if (trackFile.TryGetProperty("title", out var titleElement))
                    {
                        var rawTitle = titleElement.GetString();
                        title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                    }

                    // Skip audio descriptions
                    if (title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Create track with URL params
                    // For dramas, pub should be the section code (e.g., "iaze"), not the publication code
                    var trackUrlParams = new List<UrlParam>
                    {
                        new UrlParam
                        {
                            Key = "pub",
                            Value = sectionCode,
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "fileformat",
                            Value = "mp3",
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "alllangs",
                            Value = "0",
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "langwritten",
                            Value = normalizedLanguageCode,
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "track",
                            Value = originalTrackNumber.ToString(),
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        }
                    };

                    var track = new BiblePublicationTrack
                    {
                        Number = trackNumber,
                        Title = title,
                        UrlParams = trackUrlParams
                    };

                    allTracks.Add(track);
                    trackNumber++;
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                logger.Debug("Section {SectionCode} not available for drama {PublicationCode} in English",
                    sectionCode, normalizedPublicationCode);
                continue;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to fetch section {SectionCode} for drama {PublicationCode} in English",
                    sectionCode, normalizedPublicationCode);
                continue;
            }
        }

        if (allTracks.Count == 0)
        {
            logger.Warning("No tracks found for drama {PublicationCode} in English", normalizedPublicationCode);
            return false;
        }

        // Create publication with flat tracks (no sections)
        // Use case-sensitive publication code for dramas: "Dramas" or "DramaticBibleReadings"
        var publicationCodeForDb = categoryKey; // categoryKey already has correct case
        var publicationName = localizedPubName ?? publicationCodeForDb;
        var publication = new BiblePublication
        {
            Code = publicationCodeForDb,
            Name = publicationName,
            Language = language,
            Category = category,
            CategoryId = category.Id,
            LanguageId = language.Id,
            IsVideo = false,
            Tracks = allTracks,
            Sections = new List<BiblePublicationSection>()
        };

        // Set publication reference on tracks
        foreach (var track in allTracks)
        {
            track.Publication = publication;
            track.Section = null; // Dramas have flat structure, no sections
        }

        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully seeded {Count} tracks for English drama {PublicationCode}",
            allTracks.Count, normalizedPublicationCode);

        return true;
    }

    private async Task<bool> FetchDramaPublicationTracksAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        BiblePublication englishPublication,
        CancellationToken cancellationToken)
    {
        // Get BaseUrl
        var baseUrl = await db.BaseUrls
            .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .FirstOrDefaultAsync(cancellationToken);

        if (baseUrl == null)
        {
            logger.Warning("No BaseUrl found");
            return false;
        }

        // Use Mediator API to get the category (case-sensitive: "Dramas" or "DramaticBibleReadings")
        // Note: normalizedPublicationCode is lowercase, but API requires exact case
        string categoryKey;
        if (normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase))
        {
            categoryKey = "Dramas";
        }
        else if (normalizedPublicationCode.Equals("dramaticbiblereadings", StringComparison.OrdinalIgnoreCase))
        {
            categoryKey = "DramaticBibleReadings";
        }
        else
        {
            logger.Warning("Unknown drama publication code: {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        var mediatorUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{normalizedLanguageCode}/{categoryKey}?detailed=1";
        
        var response = await httpClient.GetAsync(mediatorUrl, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            logger.Warning("Failed to fetch drama category {CategoryKey} for language {LanguageCode}: {StatusCode}",
                categoryKey, normalizedLanguageCode, response.StatusCode);
            return false;
        }

        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("category", out var categoryElement))
        {
            logger.Warning("Invalid response structure for drama category {CategoryKey}", categoryKey);
            return false;
        }

        // Extract localized publication name
        string? localizedPubName = null;
        if (categoryElement.TryGetProperty("name", out var nameElement))
        {
            var rawName = nameElement.GetString();
            localizedPubName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
        }

        // Extract section codes from category.media array
        if (!categoryElement.TryGetProperty("media", out var mediaArray) || mediaArray.ValueKind != JsonValueKind.Array)
        {
            logger.Warning("No media items found in drama category {CategoryKey}", categoryKey);
            return false;
        }

        var sectionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mediaItem in mediaArray.EnumerateArray())
        {
            // Extract section code from naturalKey: "pub-{sectionCode}_{lang}_{number}_AUDIO"
            if (mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
            {
                var naturalKey = naturalKeyElement.GetString() ?? "";
                if (naturalKey.StartsWith("pub-", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = naturalKey.Split('_');
                    if (parts.Length > 0)
                    {
                        var sectionCode = parts[0].Substring(4); // Remove "pub-" prefix
                        if (!string.IsNullOrEmpty(sectionCode))
                        {
                            sectionCodes.Add(sectionCode);
                        }
                    }
                }
            }
        }

        if (sectionCodes.Count == 0)
        {
            logger.Warning("No section codes found in drama category {CategoryKey}", categoryKey);
            return false;
        }

        // Fetch tracks for each section using GETPUBMEDIALINKS
        var allTracks = new List<BiblePublicationTrack>();
        var trackNumber = 1;

        foreach (var sectionCode in sectionCodes.OrderBy(sc => sc))
        {
            try
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";
                var sectionResponse = await httpClient.GetAsync(harvestLink, cancellationToken);
                
                if (!sectionResponse.IsSuccessStatusCode)
                {
                    logger.Debug("Section {SectionCode} not available for drama {PublicationCode} in language {LanguageCode}",
                        sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                    continue;
                }

                var sectionJsonString = await sectionResponse.Content.ReadAsStringAsync(cancellationToken);
                using var sectionDoc = JsonDocument.Parse(sectionJsonString);
                var sectionRoot = sectionDoc.RootElement;

                if (sectionRoot.ValueKind != JsonValueKind.Object || !sectionRoot.TryGetProperty("files", out var sectionFilesElement))
                {
                    continue;
                }

                if (!sectionFilesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
                    !languageFiles.TryGetProperty("MP3", out var mp3Files))
                {
                    continue;
                }

                foreach (var trackFile in mp3Files.EnumerateArray())
                {
                    if (!trackFile.TryGetProperty("file", out var fileElement) ||
                        !fileElement.TryGetProperty("url", out var urlElement))
                    {
                        continue;
                    }

                    var url = urlElement.GetString();
                    if (string.IsNullOrEmpty(url))
                    {
                        continue;
                    }

                    // Get track number from API (original track number within the section)
                    int originalTrackNumber = 0;
                    if (trackFile.TryGetProperty("track", out var trackElement))
                    {
                        originalTrackNumber = trackElement.GetInt32();
                    }

                    if (originalTrackNumber == 0)
                    {
                        continue;
                    }

                    // Get title
                    string title = "Unknown";
                    if (trackFile.TryGetProperty("title", out var titleElement))
                    {
                        var rawTitle = titleElement.GetString();
                        title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                    }

                    // Skip audio descriptions
                    if (title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Create track with URL params
                    // For dramas, pub should be the section code (e.g., "iaze"), not the publication code
                    var trackUrlParams = new List<UrlParam>
                    {
                        new UrlParam
                        {
                            Key = "pub",
                            Value = sectionCode,
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "fileformat",
                            Value = "mp3",
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "alllangs",
                            Value = "0",
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "langwritten",
                            Value = normalizedLanguageCode,
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new UrlParam
                        {
                            Key = "track",
                            Value = originalTrackNumber.ToString(),
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        }
                    };

                    var track = new BiblePublicationTrack
                    {
                        Number = trackNumber,
                        Title = title,
                        UrlParams = trackUrlParams
                    };

                    allTracks.Add(track);
                    trackNumber++;
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                logger.Debug("Section {SectionCode} not available for drama {PublicationCode} in language {LanguageCode}",
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                continue;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to fetch section {SectionCode} for drama {PublicationCode} in language {LanguageCode}",
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                continue;
            }
        }

        if (allTracks.Count == 0)
        {
            logger.Warning("No tracks found for drama {PublicationCode} in language {LanguageCode}", normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        // Get language and category
        var language = await db.Languages
            .FirstOrDefaultAsync(l => l.Code == normalizedLanguageCode, cancellationToken);
        
        if (language == null)
        {
            logger.Warning("Language {LanguageCode} not found", normalizedLanguageCode);
            return false;
        }

        var category = englishPublication.Category;
        if (category == null)
        {
            logger.Warning("Category not found for English publication {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        // Create publication with flat tracks (no sections)
        // Use case-sensitive publication code for dramas: "Dramas" or "DramaticBibleReadings"
        var publicationCodeForDb = categoryKey; // categoryKey already has correct case
        var publicationName = localizedPubName ?? publicationCodeForDb;
        var publication = new BiblePublication
        {
            Code = publicationCodeForDb,
            Name = publicationName,
            Language = language,
            Category = category,
            CategoryId = category.Id,
            LanguageId = language.Id,
            IsVideo = false,
            Tracks = allTracks,
            Sections = new List<BiblePublicationSection>()
        };

        // Set publication reference on tracks
        foreach (var track in allTracks)
        {
            track.Publication = publication;
            track.Section = null; // Dramas have flat structure, no sections
        }

        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully fetched {Count} tracks for drama {PublicationCode} in language {LanguageCode}",
            allTracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        return true;
    }
}
