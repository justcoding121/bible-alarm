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
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for fetching flat-track publications (Music and Video).
/// </summary>
internal sealed class FlatPublicationFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly VideoLocalizedNameFetcher videoLocalizedNameFetcher;

    public FlatPublicationFetcher(HttpClient httpClient, ILogger logger, VideoLocalizedNameFetcher videoLocalizedNameFetcher)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.videoLocalizedNameFetcher = videoLocalizedNameFetcher ?? throw new ArgumentNullException(nameof(videoLocalizedNameFetcher));
    }

    /// <summary>
    /// Unified method for fetching flat-track publications (Music and Video).
    /// Handles both MP3 (Music) and MP4 (Video) formats with their specific behaviors.
    /// </summary>
    public async Task<bool> FetchFlatPublicationTracksAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        BiblePublication englishPublication,
        bool isVideo,
        bool isMusic,
        string fileFormat,
        string trackParam,
        Language? language = null,
        CancellationToken cancellationToken = default)
    {
        var tracks = new List<BiblePublicationTrack>();
        string? localizedPubName = null;
        
        // For videos, fetch localized name from Mediator API (similar to dramas)
        // The GETPUBMEDIALINKS API might return English name even when requesting other languages
        if (isVideo && !normalizedLanguageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            localizedPubName = await videoLocalizedNameFetcher.FetchVideoLocalizedNameFromMediatorAsync(
                normalizedPublicationCode, normalizedLanguageCode, cancellationToken);
        }
        
        var trackCode = 1;
        var consecutiveFailures = 0;
        const int MaxConsecutiveFailures = 3;

        while (consecutiveFailures < MaxConsecutiveFailures)
        {
            try
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&fileformat={fileFormat}&alllangs=0{trackParam}{trackCode}&langwritten={normalizedLanguageCode}";
                var response = await httpClient.GetAsync(harvestLink, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    consecutiveFailures++;
                    trackCode++;
                    continue;
                }

                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
                {
                    consecutiveFailures++;
                    trackCode++;
                    continue;
                }

                if (!filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles))
                {
                    consecutiveFailures++;
                    trackCode++;
                    continue;
                }

                if (!languageFiles.TryGetProperty(fileFormat, out var formatFiles))
                {
                    consecutiveFailures++;
                    trackCode++;
                    continue;
                }

                // Extract localized publication name (only once)
                // For videos, we already tried Mediator API first, so only use pubName as fallback
                // For videos, validate that pubName is not in English when requesting non-English language
                if (localizedPubName == null && root.TryGetProperty("pubName", out var pubNameElement))
                {
                    var rawName = pubNameElement.GetString();
                    var extractedName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                    
                    // For videos, if we're requesting a non-English language and the name is in English, skip it
                    // This prevents using English names for non-English languages
                    if (isVideo && !normalizedLanguageCode.Equals("E", StringComparison.OrdinalIgnoreCase) && 
                        !string.IsNullOrEmpty(extractedName))
                    {
                        // Check if the name contains known English video names
                        var knownEnglishNames = new[] { "The Good News According to Jesus", "Good news according to Jesus" };
                        var isEnglishName = knownEnglishNames.Any(en => extractedName.Contains(en, StringComparison.OrdinalIgnoreCase));
                        
                        if (isEnglishName)
                        {
                            logger.Debug("API returned English name '{ExtractedName}' for video {PublicationCode} in language {LanguageCode}. Skipping and using fallback.",
                                extractedName, normalizedPublicationCode, normalizedLanguageCode);
                            extractedName = null; // Don't use the English name
                        }
                    }
                    
                    localizedPubName = extractedName;
                }

                // Get ApiUrl for tracks
                var apiUrl = await db.ApiUrls
                    .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
                    .FirstOrDefaultAsync(cancellationToken);

                if (apiUrl == null)
                {
                    logger.Warning("No ApiUrl found for publication tracks");
                    return false;
                }

                // Process files based on type
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

                                var trackUrlParams = new List<UrlParam>
                                {
                                    new UrlParam
                                    {
                                        Key = "pub",
                                        Value = normalizedPublicationCode,
                                        IsQueryParam = true,
                                        ApiUrl = apiUrl,
                                        ApiUrlId = apiUrl.Id
                                    },
                                    new UrlParam
                                    {
                                        Key = "track",
                                        Value = trackCode.ToString(),
                                        IsQueryParam = true,
                                        ApiUrl = apiUrl,
                                        ApiUrlId = apiUrl.Id
                                    },
                                    new UrlParam
                                    {
                                        Key = "fileformat",
                                        Value = fileFormat.ToLowerInvariant(),
                                        IsQueryParam = true,
                                        ApiUrl = apiUrl,
                                        ApiUrlId = apiUrl.Id
                                    },
                                    new UrlParam
                                    {
                                        Key = "alllangs",
                                        Value = "0",
                                        IsQueryParam = true,
                                        ApiUrl = apiUrl,
                                        ApiUrlId = apiUrl.Id
                                    },
                                    new UrlParam
                                    {
                                        Key = "langwritten",
                                        Value = normalizedLanguageCode,
                                        IsQueryParam = true,
                                        ApiUrl = apiUrl,
                                        ApiUrlId = apiUrl.Id
                                    }
                                };

                                // TrackCode is the track param value from URL params
                                var track = new BiblePublicationTrack
                                {
                                    TrackCode = trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                    Title = title,
                                    UrlParams = trackUrlParams
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

                        var apiTrackCode = trackElement.GetInt32();
                        if (apiTrackCode == 0 || url.EndsWith(".zip"))
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

                        var trackUrlParams = new List<UrlParam>
                        {
                            new UrlParam
                            {
                                Key = "pub",
                                Value = normalizedPublicationCode,
                                IsQueryParam = true,
                                ApiUrl = apiUrl,
                                ApiUrlId = apiUrl.Id
                            },
                            new UrlParam
                            {
                                Key = "track",
                                Value = apiTrackCode.ToString(),
                                IsQueryParam = true,
                                ApiUrl = apiUrl,
                                ApiUrlId = apiUrl.Id
                            },
                            new UrlParam
                            {
                                Key = "fileformat",
                                Value = fileFormat.ToLowerInvariant(),
                                IsQueryParam = true,
                                ApiUrl = apiUrl,
                                ApiUrlId = apiUrl.Id
                            },
                            new UrlParam
                            {
                                Key = "alllangs",
                                Value = "0",
                                IsQueryParam = true,
                                ApiUrl = apiUrl,
                                ApiUrlId = apiUrl.Id
                            },
                            new UrlParam
                            {
                                Key = "langwritten",
                                Value = normalizedLanguageCode,
                                IsQueryParam = true,
                                ApiUrl = apiUrl,
                                ApiUrlId = apiUrl.Id
                            }
                        };

                        // TrackCode is the apiTrackCode from the API response (track param value)
                        var bibleTrack = new BiblePublicationTrack
                        {
                            TrackCode = apiTrackCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            Title = title,
                            UrlParams = trackUrlParams
                        };
                        tracks.Add(bibleTrack);
                        trackCode++;
                    }
                    consecutiveFailures = 0;
                    break; // Music returns all tracks in one response
                }

                trackCode++;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                consecutiveFailures++;
                trackCode++;
                continue;
            }
            catch (Exception ex)
            {
                if (NetworkExceptionHelper.IsNetworkFailure(ex))
                {
                    throw;
                }

                logger.Warning(ex, "Failed to fetch track {TrackCode} for publication {PublicationCode} in language {LanguageCode}",
                    trackCode, normalizedPublicationCode, normalizedLanguageCode);
                consecutiveFailures++;
                trackCode++;
                continue;
            }
        }

        if (tracks.Count == 0)
        {
            logger.Warning("No tracks found for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        // Get or create language and category
        Language? resolvedLanguage = language;
        if (resolvedLanguage == null)
        {
            resolvedLanguage = await db.Languages
                .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);
            
            if (resolvedLanguage == null)
            {
                logger.Warning("Language {LanguageCode} not found in database", normalizedLanguageCode);
                return false;
            }
        }

        var category = englishPublication.Category;
        if (category == null)
        {
            logger.Warning("Category not found for English publication {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        // Check if publication already exists
        // Handle null language case separately since EF Core can't translate null propagating operator
        BiblePublication? existingPublication;
        if (resolvedLanguage != null)
        {
            existingPublication = await db.BiblePublications
                .Include(bp => bp.Tracks)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == normalizedPublicationCode &&
                          bp.LanguageId == resolvedLanguage.Id,
                    cancellationToken);
        }
        else
        {
            existingPublication = await db.BiblePublications
                .Include(bp => bp.Tracks)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == normalizedPublicationCode &&
                          bp.LanguageId == null,
                    cancellationToken);
        }

        if (existingPublication != null)
        {
            // Publication exists - delete it and its tracks to avoid duplicates
            // We'll replace it with the new one that has all tracks
            logger.Information("Publication {PublicationCode} already exists for language {LanguageCode}, replacing with updated tracks",
                normalizedPublicationCode, normalizedLanguageCode ?? "(null)");
            
            // Remove existing tracks (cascade delete will handle sections if any)
            db.BiblePublicationTracks.RemoveRange(existingPublication.Tracks);
            await db.SaveChangesAsync(cancellationToken);
            
            // Remove the publication
            db.BiblePublications.Remove(existingPublication);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Create publication
        var publicationName = localizedPubName ?? englishPublication.Name;
        var publication = new BiblePublication
        {
            PublicationCode = normalizedPublicationCode,
            Name = publicationName,
            Language = resolvedLanguage,
            Category = category,
            CategoryId = category.Id,
            LanguageId = resolvedLanguage?.Id,
            IsVideo = isVideo,
            Tracks = tracks,
            Sections = new List<BiblePublicationSection>()
        };

        // Set publication reference on tracks
        foreach (var track in tracks)
        {
            track.Publication = publication;
        }

        // Add ApiUrl reference to tracks via UrlParams (already set above)
        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully fetched {Count} tracks for publication {PublicationCode} in language {LanguageCode}",
            tracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        return true;
    }
}
