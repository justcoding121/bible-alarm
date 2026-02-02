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
internal sealed class SectionFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly EnglishTrackParser trackParser;
    private readonly DramaTrackParser dramaTrackParser;

    public SectionFetcher(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.trackParser = new EnglishTrackParser(logger);
        this.dramaTrackParser = new DramaTrackParser(logger);
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
        var language = await db.Languages.FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);
        
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

        var baseUrl = await db.BaseUrls.Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS").FirstOrDefaultAsync(cancellationToken);

        if (baseUrl == null)
        {
            logger.Warning("No BaseUrl found");
            return false;
        }

        var isBible = category.CategoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase);
        var sections = new List<BiblePublicationSection>();
        string? localizedPubName = null;

        foreach (var sectionCode in sectionCodes)
        {
            try
            {
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

                string? sectionName = null;
                if (root.TryGetProperty("pubName", out var pubNameElement))
                {
                    var rawName = pubNameElement.GetString();
                    sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                }
                if (sectionName == null)
                {
                    logger.Debug("Section name (pubName) not found in API response for section {SectionCode} in language {LanguageCode}",
                        sectionCode, normalizedLanguageCode);
                }

                if (localizedPubName == null && root.TryGetProperty("parentPubName", out var parentPubNameElement))
                {
                    var rawName = parentPubNameElement.GetString();
                    var extractedName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                    
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

                var tracks = new List<BiblePublicationTrack>();
                if (isBible)
                {
                    tracks = trackParser.ParseBibleTracks(
                        filesElement, normalizedLanguageCode, normalizedPublicationCode, sectionCode, baseUrl);
                }
                else
                {
                    int nextTrackNumber;
                    tracks = dramaTrackParser.ParseTracksFromJson(
                        filesElement, normalizedLanguageCode, sectionCode, baseUrl, startTrackNumber: 1, out nextTrackNumber);
                }

                var section = new BiblePublicationSection
                {
                    Name = sectionName ?? sectionCode,
                    SectionCode = sectionCode.ToLowerInvariant(), // Use section code from API
                    Tracks = tracks
                };

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

        var existingPublication = await db.BiblePublications.Include(bp => bp.Sections).FirstOrDefaultAsync(
                bp => bp.PublicationCode == normalizedPublicationCode &&
                      bp.LanguageId == language.Id,
                cancellationToken);

        BiblePublication publication;
        if (existingPublication != null)
        {
            logger.Information("Publication {PublicationCode} already exists for language {LanguageCode}, replacing with updated sections",
                normalizedPublicationCode, normalizedLanguageCode);
            
            db.BiblePublicationSections.RemoveRange(existingPublication.Sections);
            await db.SaveChangesAsync(cancellationToken);
            
            db.BiblePublications.Remove(existingPublication);
            await db.SaveChangesAsync(cancellationToken);
        }

        var tracksBySection = new Dictionary<BiblePublicationSection, List<BiblePublicationTrack>>();
        foreach (var section in sections)
        {
            if (section.Tracks.Count > 0)
            {
                tracksBySection[section] = section.Tracks.ToList();
                section.Tracks.Clear();
            }
        }

        var publicationName = localizedPubName ?? englishPublication.Name;
        publication = new BiblePublication
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

        foreach (var section in sections)
        {
            section.BiblePublication = publication;
        }

        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var kvp in tracksBySection)
        {
            var section = kvp.Key;
            var tracks = kvp.Value;
            
            foreach (var track in tracks)
            {
                track.Section = section;
                track.Publication = publication;
            }
            
            section.Tracks.AddRange(tracks);
        }

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
        await db.Entry(section).Collection(s => s.Tracks).LoadAsync(cancellationToken);
        if (section.Tracks != null && section.Tracks.Count > 0)
        {
            logger.Debug("Tracks already exist for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}, skipping fetch",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return true;
        }

        var categoryName = publication.Category?.CategoryName ?? "";
        var isBible = categoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase);

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

        var baseUrl = await db.BaseUrls.Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS").FirstOrDefaultAsync(cancellationToken);

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
                
                if (isBible && !string.IsNullOrEmpty(title) && title != "Unknown")
                {
                    var separators = new[] { " - ", " – ", " — ", " -", "- " };
                    foreach (var separator in separators)
                    {
                        if (title.Contains(separator))
                        {
                            var parts = title.Split(new[] { separator }, StringSplitOptions.None);
                            if (parts.Length > 1)
                            {
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

        section.Tracks = tracks;
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully fetched {Count} tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
            tracks.Count, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);

        return true;
    }
}
