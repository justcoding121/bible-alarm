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

                // Get BaseUrl for tracks
                var baseUrl = await db.BaseUrls
                    .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
                    .FirstOrDefaultAsync(cancellationToken);

                if (baseUrl == null)
                {
                    logger.Warning("No BaseUrl found for publication tracks");
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
                                    }
                                };

                                var track = new BiblePublicationTrack
                                {
                                    Number = trackNumber,
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

                        var apiTrackNumber = trackElement.GetInt32();
                        if (apiTrackNumber == 0 || url.EndsWith(".zip"))
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
                                BaseUrl = baseUrl,
                                BaseUrlId = baseUrl.Id
                            },
                            new UrlParam
                            {
                                Key = "track",
                                Value = apiTrackNumber.ToString(),
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
                            }
                        };

                        var bibleTrack = new BiblePublicationTrack
                        {
                            Number = trackNumber,
                            Title = title,
                            UrlParams = trackUrlParams
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
                    trackNumber, normalizedPublicationCode, normalizedLanguageCode);
                consecutiveFailures++;
                trackNumber++;
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

        // Add BaseUrl reference to tracks via UrlParams (already set above)
        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully fetched {Count} tracks for publication {PublicationCode} in language {LanguageCode}",
            tracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        return true;
    }
}
