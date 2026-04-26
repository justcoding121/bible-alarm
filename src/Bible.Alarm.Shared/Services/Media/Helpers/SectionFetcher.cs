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
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class SectionFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly EnglishTrackParser trackParser;
    private readonly MediatorTrackParser mediatorTrackParser;
    private readonly SectionFetcherSectionTracksLoader sectionTracksLoader;

    public SectionFetcher(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.trackParser = new EnglishTrackParser();
        this.mediatorTrackParser = new MediatorTrackParser();
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

        var category = englishPublication.PrimaryCategory;
        if (category == null)
        {
            logger.Warning("Category not found for English publication {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        var categoryCodes = JwSourceHelper.GetCategoryCodesForPublication(normalizedPublicationCode);
        var categoriesForPub = await db.Categories
            .Where(c => categoryCodes.Contains(c.CategoryCode))
            .ToListAsync(effectiveToken);
        if (categoriesForPub.Count == 0)
        {
            logger.Warning("No categories found for publication {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        var existingPublication = await db.BiblePublications
            .Include(bp => bp.Sections)
            .Include(bp => bp.BiblePublicationCategories)
            .ThenInclude(bpc => bpc.Category)
            .FirstOrDefaultAsync(
                bp => bp.PublicationCode == normalizedPublicationCode &&
                      bp.LanguageId == language.Id,
                effectiveToken);

        var existingSectionCodes = existingPublication?.Sections
            .Select(s => s.SectionCode.ToLowerInvariant())
            .ToHashSet() ?? new HashSet<string>();

        var missingSectionCodes = sectionCodes
            .Where(sc => !existingSectionCodes.Contains(sc.ToLowerInvariant()))
            .ToList();

        if (missingSectionCodes.Count == 0 && existingPublication != null)
        {
            SyncPublicationCategories(existingPublication, categoriesForPub);
            await SaveChangesWithRetryAsync(db, effectiveToken);
            logger.Debug("All sections already exist for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            progress?.UpdateProgress(1.0);
            return true;
        }

        var isBible = category.CategoryCode.Equals("Bible", StringComparison.OrdinalIgnoreCase);
        var isIssueSectioned = MagazineHelper.IsMagazinePublicationCode(normalizedPublicationCode);
        var determinedCatalogType = PublicationTypeHelper.GetCatalogType(normalizedPublicationCode);
        string? localizedPubName = null;

        var isMusicPub = categoriesForPub.Any(c => c.CategoryCode.Equals("Music", StringComparison.OrdinalIgnoreCase)) ||
            JwSourceHelper.MusicFlagPublicationCodes.Contains(normalizedPublicationCode);
        BiblePublication publication;
        if (existingPublication != null)
        {
            publication = existingPublication;
            publication.IsMusic = isMusicPub;
            if (publication.CatalogType == null)
            {
                publication.CatalogType = determinedCatalogType;
            }
            SyncPublicationCategories(publication, categoriesForPub);
        }
        else
        {
            var isVideoDrama = PublicationTypeHelper.IsVideo(normalizedPublicationCode);
            publication = new BiblePublication
            {
                PublicationCode = normalizedPublicationCode,
                Name = englishPublication.Name,
                Language = language,
                BiblePublicationCategories = categoriesForPub
                    .Select(cat => new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = cat.Id, Category = cat })
                    .ToList(),
                LanguageId = language.Id,
                IsVideo = isVideoDrama,
                IsMusic = isMusicPub,
                CatalogType = determinedCatalogType,
                Tracks = new List<BiblePublicationTrack>(),
                Sections = new List<BiblePublicationSection>()
            };
            db.BiblePublications.Add(publication);
            await SaveChangesWithRetryAsync(db, effectiveToken);
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
                var dramaFileFormat = !isBible && !isIssueSectioned && PublicationTypeHelper.IsVideo(normalizedPublicationCode) ? "MP4" : "MP3";
                string queryString;
                if (isIssueSectioned)
                {
                    var (apiPubCode, issueCode) = MagazineHelper.ParseSectionCode(sectionCode);
                    queryString = $"?output=json&pub={apiPubCode}&issue={issueCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";
                }
                else if (isBible)
                {
                    queryString = $"?output=json&pub={normalizedPublicationCode}&booknum={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";
                }
                else
                {
                    queryString = $"?output=json&pub={sectionCode}&fileformat={dramaFileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
                }

                var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
                var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, effectiveToken);
                if (jsonString == null)
                {
                    logger.Debug("Section {SectionCode} not available for publication {PublicationCode} in language {LanguageCode}",
                        sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                    completedSections++;
                    continue;
                }
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
                {
                    completedSections++;
                    continue;
                }

                string? sectionName = null;
                if (isIssueSectioned)
                {
                    string? pubName = null;
                    string? formattedDate = null;
                    if (root.TryGetProperty("pubName", out var pnEl))
                        pubName = pnEl.GetString();
                    if (root.TryGetProperty("formattedDate", out var fdEl))
                        formattedDate = fdEl.GetString();
                    sectionName = MagazineHelper.BuildSectionName(pubName, formattedDate);
                }
                else if (root.TryGetProperty("pubName", out var pubNameElement))
                {
                    var rawName = pubNameElement.GetString();
                    sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                }
                if (sectionName == null)
                {
                    logger.Debug("Section name not found in API response for section {SectionCode} in language {LanguageCode}",
                        sectionCode, normalizedLanguageCode);
                }

                // Extract localized publication name from first section response
                if (isIssueSectioned && localizedPubName == null)
                {
                    localizedPubName = MagazineHelper.GetYear(normalizedPublicationCode).ToString();
                    publication.Name = localizedPubName;
                }
                else if (localizedPubName == null && root.TryGetProperty("parentPubName", out var parentPubNameElement))
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
                if (isIssueSectioned)
                {
                    tracks = trackParser.ParseGenericTracks(
                        filesElement, normalizedLanguageCode, "MP3", sectionCode);
                }
                else if (isBible)
                {
                    tracks = trackParser.ParseBibleTracks(
                        filesElement, normalizedLanguageCode, normalizedPublicationCode, sectionCode);
                }
                else
                {
                    var isVideoDrama = PublicationTypeHelper.IsVideo(normalizedPublicationCode);
                    tracks = mediatorTrackParser.ParseTracksFromJson(
                        filesElement, normalizedLanguageCode, sectionCode, isVideoDrama);
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
                await SaveChangesWithRetryAsync(db, effectiveToken);

                // Add tracks to section
                foreach (var track in tracks)
                {
                    track.Section = section;
                    track.Publication = publication;
                }
                section.Tracks.AddRange(tracks);
                await SaveChangesWithRetryAsync(db, effectiveToken);

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
            await SaveChangesWithRetryAsync(db, effectiveToken);
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
        CancellationToken cancellationToken,
        bool replaceExisting = false)
    {
        return sectionTracksLoader.FetchSectionTracksAsync(db, normalizedPublicationCode, normalizedSectionCode, normalizedLanguageCode, publicationCodeForDb, publication, section, cancellationToken, replaceExisting);
    }

    /// <summary>
    /// Saves changes with retry on SQLite busy/locked (transient lock contention during section catalog).
    /// </summary>
    private async Task SaveChangesWithRetryAsync(MediaDbContext db, CancellationToken cancellationToken)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts)
            {
                if (IsSqliteBusyOrLocked(ex))
                {
                    logger.Debug("SaveChanges failed with database locked (attempt {Attempt}/{Max}), retrying",
                        attempt, maxAttempts);
                    await Task.Delay(100 * attempt, cancellationToken);
                    continue;
                }
                throw;
            }
        }
    }

    private static bool IsSqliteBusyOrLocked(Exception ex)
    {
        const int sqliteBusy = 5;
        const int sqliteLocked = 6;
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SqliteException sqliteEx)
            {
                var code = (int)sqliteEx.SqliteErrorCode;
                if (code == sqliteBusy || code == sqliteLocked)
                {
                    return true;
                }
            }
        }
        return false;
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
}
