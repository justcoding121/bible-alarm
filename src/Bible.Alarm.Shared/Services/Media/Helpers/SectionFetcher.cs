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
/// Helper class for fetching publication sections and section tracks.
/// </summary>
internal sealed class SectionFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;

    public SectionFetcher(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> FetchPublicationSectionsAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        string publicationCodeForDb,
        BiblePublication englishPublication,
        List<string> sectionCodes,
        CancellationToken cancellationToken)
    {
        // Get or create language and category
        var language = await db.Languages
            .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);
        
        if (language == null)
        {
            logger.Warning("Language {LanguageCode} not found in database", normalizedLanguageCode);
            return false;
        }

        var category = englishPublication.Category;
        if (category == null)
        {
            logger.Warning("Category not found for English publication {PublicationCode}", normalizedPublicationCode);
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
                        sectionCode, normalizedPublicationCode, normalizedLanguageCode);
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
                    var extractedName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                    
                    // Validate that the extracted name is not a known video/drama publication name
                    // This prevents contamination from wrong API responses
                    if (!string.IsNullOrEmpty(extractedName) && isBible)
                    {
                        var knownVideoNames = new[] { "The Good News According to Jesus", "Good news according to Jesus" };
                        var isVideoName = knownVideoNames.Any(vn => extractedName.Contains(vn, StringComparison.OrdinalIgnoreCase));
                        
                        if (isVideoName)
                        {
                            logger.Warning("API returned video/drama publication name '{ExtractedName}' for Bible publication {PublicationCode} in language {LanguageCode}. This is likely an API error. Skipping this name and using fallback.",
                                extractedName, normalizedPublicationCode, normalizedLanguageCode);
                            extractedName = null; // Don't use the wrong name
                        }
                    }
                    
                    localizedPubName = extractedName;
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
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                continue;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to fetch section {SectionCode} for publication {PublicationCode} in language {LanguageCode}",
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                continue;
            }
        }

        if (sections.Count == 0)
        {
            logger.Warning("No sections found for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        // Create publication
        var publicationName = localizedPubName ?? englishPublication.Name;
        var publication = new BiblePublication
        {
            PublicationCode = normalizedPublicationCode,
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
            sections.Count, normalizedPublicationCode, normalizedLanguageCode);

        return true;
    }

    public async Task<bool> FetchSectionTracksAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedSectionCode,
        string normalizedLanguageCode,
        string publicationCodeForDb,
        BiblePublication publication,
        BiblePublicationSection section,
        CancellationToken cancellationToken)
    {
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
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
        {
            logger.Warning("Invalid response format for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        if (!filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
            !languageFiles.TryGetProperty("MP3", out var mp3Files))
        {
            logger.Warning("No MP3 files found for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
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
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        // Add tracks to section
        section.Tracks = tracks;
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully fetched {Count} tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
            tracks.Count, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);

        return true;
    }
}
