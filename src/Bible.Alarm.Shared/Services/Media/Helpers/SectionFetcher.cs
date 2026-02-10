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
        CancellationToken cancellationToken,
        IFetchProgress? progress = null)
    {
        // Use progress token if available, otherwise use provided token
        var effectiveToken = progress?.CancellationToken ?? cancellationToken;

        var language = await db.Languages.FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, effectiveToken);
        
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

        var baseUrl = await db.BaseUrls.Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS").FirstOrDefaultAsync(effectiveToken);

        if (baseUrl == null)
        {
            logger.Warning("No BaseUrl found");
            return false;
        }

        // Check for existing publication and determine which sections already exist
        var existingPublication = await db.BiblePublications
            .Include(bp => bp.Sections)
            .FirstOrDefaultAsync(
                bp => bp.PublicationCode == normalizedPublicationCode &&
                      bp.LanguageId == language.Id,
                effectiveToken);

        var existingSectionCodes = existingPublication?.Sections
            .Select(s => s.SectionCode.ToLowerInvariant())
            .ToHashSet() ?? new HashSet<string>();

        // Filter to only missing sections (for resume support)
        var missingSectionCodes = sectionCodes
            .Where(sc => !existingSectionCodes.Contains(sc.ToLowerInvariant()))
            .ToList();

        // If all sections exist, we're done
        if (missingSectionCodes.Count == 0 && existingPublication != null)
        {
            logger.Debug("All sections already exist for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            progress?.UpdateProgress(1.0);
            return true;
        }

        var isBible = category.CategoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase);
        string? localizedPubName = null;

        // Create or get the publication first (so we can add sections incrementally)
        BiblePublication publication;
        if (existingPublication != null)
        {
            publication = existingPublication;
            // Update publication name if we get a localized one later
        }
        else
        {
            // Create a new publication with empty sections
            publication = new BiblePublication
            {
                PublicationCode = normalizedPublicationCode,
                Name = englishPublication.Name, // Will update if we get localized name
                Language = language,
                Category = category,
                CategoryId = category.Id,
                LanguageId = language.Id,
                IsVideo = false,
                Tracks = new List<BiblePublicationTrack>(),
                Sections = new List<BiblePublicationSection>()
            };
            db.BiblePublications.Add(publication);
            await db.SaveChangesAsync(effectiveToken);
        }

        var totalSections = missingSectionCodes.Count;
        var completedSections = 0;

        // Fetch and save sections one by one (incremental saves)
        foreach (var sectionCode in missingSectionCodes)
        {
            // Check for cancellation before each section
            effectiveToken.ThrowIfCancellationRequested();

            try
            {
                var harvestLink = isBible
                    ? $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&booknum={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}"
                    : $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";

                var response = await httpClient.GetAsync(harvestLink, effectiveToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    logger.Debug("Section {SectionCode} not available for publication {PublicationCode} in language {LanguageCode}",
                        sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                    completedSections++;
                    continue;
                }

                var jsonString = await response.Content.ReadAsStringAsync(effectiveToken);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
                {
                    completedSections++;
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

                // Extract localized publication name from first section response
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
                            extractedName = null;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(extractedName))
                    {
                        localizedPubName = extractedName;
                        // Update publication name
                        publication.Name = localizedPubName;
                    }
                }

                var tracks = new List<BiblePublicationTrack>();
                if (isBible)
                {
                    tracks = trackParser.ParseBibleTracks(
                        filesElement, normalizedLanguageCode, normalizedPublicationCode, sectionCode, baseUrl);
                }
                else
                {
                    tracks = dramaTrackParser.ParseTracksFromJson(
                        filesElement, normalizedLanguageCode, sectionCode, baseUrl);
                }

                // Create and save section immediately (incremental save)
                var section = new BiblePublicationSection
                {
                    Name = sectionName ?? sectionCode,
                    SectionCode = sectionCode.ToLowerInvariant(),
                    BiblePublication = publication,
                    Tracks = new List<BiblePublicationTrack>()
                };

                publication.Sections.Add(section);
                await db.SaveChangesAsync(effectiveToken);

                // Add tracks to section
                foreach (var track in tracks)
                {
                    track.Section = section;
                    track.Publication = publication;
                }
                section.Tracks.AddRange(tracks);
                await db.SaveChangesAsync(effectiveToken);

                completedSections++;

                // Update progress AFTER successful save
                var progressPercent = (double)completedSections / totalSections;
                progress?.UpdateProgress(progressPercent);
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation - data saved so far is preserved
                logger.Information("Section fetch cancelled at {CompletedSections}/{TotalSections} for publication {PublicationCode} in language {LanguageCode}",
                    completedSections, totalSections, normalizedPublicationCode, normalizedLanguageCode);
                throw;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                logger.Debug("Section {SectionCode} not available for publication {PublicationCode} in language {LanguageCode}",
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                completedSections++;
                continue;
            }
            catch (Exception ex)
            {
                if (NetworkExceptionHelper.IsNetworkFailure(ex))
                {
                    throw;
                }

                logger.Warning(ex, "Failed to fetch section {SectionCode} for publication {PublicationCode} in language {LanguageCode}",
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                completedSections++;
                continue;
            }
        }

        // Check if we have at least one section
        var totalSectionCount = publication.Sections.Count;
        if (totalSectionCount == 0)
        {
            logger.Warning("No sections found for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            // Clean up the empty publication
            db.BiblePublications.Remove(publication);
            await db.SaveChangesAsync(effectiveToken);
            return false;
        }

        logger.Information("Successfully fetched {Count} sections for publication {PublicationCode} in language {LanguageCode}",
            totalSectionCount, normalizedPublicationCode, normalizedLanguageCode);

        progress?.UpdateProgress(1.0);
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
        // Ensure section is tracked by the DbContext
        // If section was created in a different scope, we need to attach it
        var entry = db.Entry(section);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
        {
            logger.Debug("Section entity is detached, attaching to DbContext: sectionCode={SectionCode}",
                normalizedSectionCode);
            db.BiblePublicationSections.Attach(section);
        }

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

        // Extract and update section name from API response
        string? updatedSectionName = null;
        if (root.TryGetProperty("pubName", out var pubNameElement))
        {
            var rawName = pubNameElement.GetString();
            logger.Debug("Found pubName in API response for section {SectionCode}: rawName={RawName}",
                normalizedSectionCode, rawName);
            
            var sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            if (!string.IsNullOrEmpty(sectionName))
            {
                var oldName = section.Name;
                updatedSectionName = sectionName;
                section.Name = sectionName;
                // Mark the Name property as modified to ensure EF Core saves it
                db.Entry(section).Property(s => s.Name).IsModified = true;
                
                // Verify the change was detected
                var isModified = db.Entry(section).Property(s => s.Name).IsModified;
                logger.Information("Updated section name from API: {OldName} -> {NewName} for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. IsModified={IsModified}",
                    oldName, sectionName, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode, isModified);
            }
            else
            {
                logger.Warning("pubName found in API response but section name is empty after processing. rawName={RawName} for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                    rawName, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            }
        }
        else
        {
            logger.Warning("pubName not found in API response for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. Available properties: {Properties}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode, 
                string.Join(", ", root.EnumerateObject().Select(p => p.Name)));
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
        var trackCode = 1;

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

            // TrackCode will be set from URL params below (track param for Bible, pub param for drama)
            var track = new BiblePublicationTrack
            {
                TrackCode = trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture), // Will be overridden by URL param value
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
                // For dramas, TrackCode is the pub param value (sectionCode)
                track.TrackCode = normalizedSectionCode;
            }

            track.UrlParams.Add(new UrlParam
            {
                Key = "track",
                Value = trackCode.ToString(),
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
            trackCode++;
        }

        if (tracks.Count == 0)
        {
            logger.Warning("No tracks found for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        // Add tracks to the section's collection instead of replacing it
        // This preserves EF Core change tracking for the section entity
        if (section.Tracks == null)
        {
            section.Tracks = new List<BiblePublicationTrack>();
        }
        foreach (var track in tracks)
        {
            section.Tracks.Add(track);
        }
        
        // Ensure section name is preserved if it was updated from API
        // Re-apply the name update if it was set earlier (defensive check)
        if (!string.IsNullOrEmpty(updatedSectionName) && section.Name != updatedSectionName)
        {
            logger.Warning("Section name was lost during track addition, re-applying: sectionCode={SectionCode}, expectedName={ExpectedName}, currentName={CurrentName}",
                normalizedSectionCode, updatedSectionName, section.Name);
            section.Name = updatedSectionName;
        }
        
        // Ensure section entity is tracked and name changes are saved
        // Mark as modified AFTER all entity modifications to ensure name update persists
        if (!string.IsNullOrEmpty(updatedSectionName))
        {
            db.Entry(section).Property(s => s.Name).IsModified = true;
            logger.Debug("Marking section name as modified before save: sectionCode={SectionCode}, name={Name}, isModified={IsModified}",
                normalizedSectionCode, section.Name, db.Entry(section).Property(s => s.Name).IsModified);
        }
        
        await db.SaveChangesAsync(cancellationToken);

        // Reload section from database to verify name was persisted
        await db.Entry(section).ReloadAsync(cancellationToken);
        var persistedSectionName = section.Name;

        if (!string.IsNullOrEmpty(updatedSectionName) && persistedSectionName != updatedSectionName)
        {
            logger.Error("Section name was not persisted correctly! Expected: {ExpectedName}, Actual: {ActualName} for section {SectionCode}",
                updatedSectionName, persistedSectionName, normalizedSectionCode);
        }

        logger.Information("Successfully fetched {Count} tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}. Section name: {SectionName}",
            tracks.Count, normalizedSectionCode, normalizedPublicationCode, normalizedLanguageCode, persistedSectionName);

        return true;
    }
}
