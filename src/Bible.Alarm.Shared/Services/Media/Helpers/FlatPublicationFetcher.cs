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
        var fetchedAllVideoTracks = false;

        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants().ToList();

        // Some video publications (e.g. thv) return all tracks in one response when no track param is used
        if (isVideo && !string.IsNullOrEmpty(trackParam))
        {
            var (allTracksResult, fetchedPubName) = await TryFetchAllVideoTracksInOneRequestAsync(
                baseUrls, normalizedPublicationCode, normalizedLanguageCode, fileFormat, cancellationToken);
            if (allTracksResult.Count > 0)
            {
                tracks.AddRange(allTracksResult);
                if (fetchedPubName != null)
                {
                    localizedPubName = fetchedPubName;
                }
                fetchedAllVideoTracks = true;
            }
        }

        while (!fetchedAllVideoTracks && consecutiveFailures < MaxConsecutiveFailures)
        {
            try
            {
                var queryString = $"?output=json&pub={normalizedPublicationCode}&fileformat={fileFormat}&alllangs=0{trackParam}{trackCode}&langwritten={normalizedLanguageCode}";
                var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, cancellationToken);
                if (jsonString == null)
                {
                    consecutiveFailures++;
                    trackCode++;
                    continue;
                }
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
                                    new UrlParam { Key = "pub", Value = normalizedPublicationCode, IsQueryParam = true },
                                    new UrlParam { Key = "track", Value = trackCode.ToString(), IsQueryParam = true },
                                    new UrlParam { Key = "fileformat", Value = fileFormat.ToLowerInvariant(), IsQueryParam = true },
                                    new UrlParam { Key = "alllangs", Value = "0", IsQueryParam = true },
                                    new UrlParam { Key = "langwritten", Value = normalizedLanguageCode, IsQueryParam = true }
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
                            new UrlParam { Key = "pub", Value = normalizedPublicationCode, IsQueryParam = true },
                            new UrlParam { Key = "track", Value = apiTrackCode.ToString(), IsQueryParam = true },
                            new UrlParam { Key = "fileformat", Value = fileFormat.ToLowerInvariant(), IsQueryParam = true },
                            new UrlParam { Key = "alllangs", Value = "0", IsQueryParam = true },
                            new UrlParam { Key = "langwritten", Value = normalizedLanguageCode, IsQueryParam = true }
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

        var categoryCodes = JwSourceHelper.GetCategoryCodesForPublication(normalizedPublicationCode);
        var categories = await db.Categories
            .Where(c => categoryCodes.Contains(c.CategoryCode))
            .ToListAsync(cancellationToken);
        if (categories.Count == 0)
        {
            logger.Warning("No categories found for publication {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        BiblePublication? existingPublication;
        if (resolvedLanguage != null)
        {
            existingPublication = await db.BiblePublications
                .Include(bp => bp.Tracks)
                .ThenInclude(t => t.UrlParams)
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bpc => bpc.Category)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == normalizedPublicationCode &&
                          bp.LanguageId == resolvedLanguage.Id,
                    cancellationToken);
        }
        else
        {
            existingPublication = await db.BiblePublications
                .Include(bp => bp.Tracks)
                .ThenInclude(t => t.UrlParams)
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bpc => bpc.Category)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == normalizedPublicationCode &&
                          bp.LanguageId == null,
                    cancellationToken);
        }

        if (existingPublication != null)
        {
            logger.Information("Publication {PublicationCode} already exists for language {LanguageCode}, updating tracks and categories",
                normalizedPublicationCode, normalizedLanguageCode ?? "(null)");
            foreach (var track in existingPublication.Tracks)
            {
                if (track.UrlParams.Count > 0)
                {
                    db.UrlParams.RemoveRange(track.UrlParams);
                }
            }
            db.BiblePublicationTracks.RemoveRange(existingPublication.Tracks);
            existingPublication.Tracks.Clear();
            existingPublication.Name = localizedPubName ?? englishPublication.Name;
            existingPublication.IsVideo = isVideo;
            SyncPublicationCategories(existingPublication, categories);
            foreach (var track in tracks)
            {
                track.Publication = existingPublication;
                track.BiblePublicationId = existingPublication.Id;
                existingPublication.Tracks.Add(track);
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var publicationName = localizedPubName ?? englishPublication.Name;
            var publication = new BiblePublication
            {
                PublicationCode = normalizedPublicationCode,
                Name = publicationName,
                Language = resolvedLanguage,
                BiblePublicationCategories = categories
                    .Select(cat => new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = cat.Id, Category = cat })
                    .ToList(),
                LanguageId = resolvedLanguage?.Id,
                IsVideo = isVideo,
                Tracks = tracks,
                Sections = new List<BiblePublicationSection>()
            };
            foreach (var track in tracks)
            {
                track.Publication = publication;
            }
            db.BiblePublications.Add(publication);
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.Information("Successfully fetched {Count} tracks for publication {PublicationCode} in language {LanguageCode}",
            tracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        return true;
    }

    private static void SyncPublicationCategories(BiblePublication publication, List<Category> categories)
    {
        var existingCategoryIds = publication.BiblePublicationCategories
            .Select(bpc => bpc.CategoryId)
            .ToHashSet();
        foreach (var cat in categories)
        {
            if (existingCategoryIds.Add(cat.Id))
            {
                publication.BiblePublicationCategories.Add(
                    new BiblePublicationCategory { BiblePublicationId = publication.Id, CategoryId = cat.Id, Category = cat });
            }
        }
    }

    /// <summary>
    /// Tries to fetch all video tracks in one request (no track param). Many video publications (e.g. thv) return
    /// all tracks in files.lang.MP4 when the track parameter is omitted. The API also supports per-track requests
    /// (track=1, track=2, …); we try fetch-all first for efficiency (one request vs many).
    /// </summary>
    private async Task<(List<BiblePublicationTrack> Tracks, string? LocalizedPubName)> TryFetchAllVideoTracksInOneRequestAsync(
        List<string> baseUrls,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        string fileFormat,
        CancellationToken cancellationToken)
    {
        var queryString = $"?output=json&pub={normalizedPublicationCode}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        try
        {
            var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, cancellationToken);
            if (jsonString == null)
            {
                return (new List<BiblePublicationTrack>(), null);
            }
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement) ||
                !filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
                !languageFiles.TryGetProperty(fileFormat, out var formatFiles) ||
                formatFiles.ValueKind != JsonValueKind.Array)
            {
                return (new List<BiblePublicationTrack>(), null);
            }

            string? fetchedPubName = null;
            if (root.TryGetProperty("pubName", out var pubNameElement))
            {
                var rawName = pubNameElement.GetString();
                fetchedPubName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            }

            var byTrack = new Dictionary<int, JsonElement>();
            foreach (var file in formatFiles.EnumerateArray())
            {
                if (!file.TryGetProperty("track", out var trackEl))
                {
                    continue;
                }

                var trackNum = trackEl.GetInt32();
                if (trackNum == 0)
                {
                    continue;
                }

                if (!byTrack.TryGetValue(trackNum, out var existing))
                {
                    byTrack[trackNum] = file;
                    continue;
                }

                var prefer240p = file.TryGetProperty("label", out var labelEl) && labelEl.GetString() == "240p";
                if (prefer240p)
                {
                    byTrack[trackNum] = file;
                }
            }

            var result = new List<BiblePublicationTrack>();
            foreach (var kv in byTrack.OrderBy(x => x.Key))
            {
                var fileElement = kv.Value;
                if (!fileElement.TryGetProperty("file", out var fileInfo) ||
                    !fileInfo.TryGetProperty("url", out var urlElement))
                {
                    continue;
                }

                var url = urlElement.GetString();
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                string title = "Unknown";
                if (fileElement.TryGetProperty("title", out var titleElement))
                {
                    var rawTitle = titleElement.GetString();
                    title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                }

                var trackUrlParams = new List<UrlParam>
                {
                    new UrlParam { Key = "pub", Value = normalizedPublicationCode, IsQueryParam = true },
                    new UrlParam { Key = "track", Value = kv.Key.ToString(), IsQueryParam = true },
                    new UrlParam { Key = "fileformat", Value = fileFormat.ToLowerInvariant(), IsQueryParam = true },
                    new UrlParam { Key = "alllangs", Value = "0", IsQueryParam = true },
                    new UrlParam { Key = "langwritten", Value = normalizedLanguageCode, IsQueryParam = true }
                };

                result.Add(new BiblePublicationTrack
                {
                    TrackCode = kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Title = title,
                    UrlParams = trackUrlParams
                });
            }

            if (result.Count > 0)
            {
                logger.Debug("Fetched {Count} video tracks in one request for publication {PublicationCode} in language {LanguageCode}",
                    result.Count, normalizedPublicationCode, normalizedLanguageCode);
            }

            return (result, fetchedPubName);
        }
        catch (Exception ex)
        {
            if (NetworkExceptionHelper.IsNetworkFailure(ex))
            {
                throw;
            }

            logger.Debug(ex, "Fetch-all (no track param) failed for publication {PublicationCode}, will try track-by-track",
                normalizedPublicationCode);
            return (new List<BiblePublicationTrack>(), null);
        }
    }
}
