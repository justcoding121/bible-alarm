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
using Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class SectionFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly EnglishTrackParser trackParser;
    private readonly DramaTrackParser dramaTrackParser;
    private readonly SectionFetcherSectionTracksLoader sectionTracksLoader;

    public SectionFetcher(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.trackParser = new EnglishTrackParser(logger);
        this.dramaTrackParser = new DramaTrackParser(logger);
        this.sectionTracksLoader = new SectionFetcherSectionTracksLoader(httpClient, logger);
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

        var apiUrl = await db.ApiUrls.Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS").FirstOrDefaultAsync(effectiveToken);

        if (apiUrl == null)
        {
            logger.Warning("No ApiUrl found");
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
                        filesElement, normalizedLanguageCode, normalizedPublicationCode, sectionCode, apiUrl);
                }
                else
                {
                    tracks = dramaTrackParser.ParseTracksFromJson(
                        filesElement, normalizedLanguageCode, sectionCode, apiUrl);
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

    public Task<bool> FetchSectionTracksAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedSectionCode,
        string normalizedLanguageCode,
        string publicationCodeForDb,
        BiblePublication publication,
        BiblePublicationSection section,
        CancellationToken cancellationToken)
    {
        return sectionTracksLoader.FetchSectionTracksAsync(db, normalizedPublicationCode, normalizedSectionCode, normalizedLanguageCode, publicationCodeForDb, publication, section, cancellationToken);
    }
}
