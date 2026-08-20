#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
using Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class SectionFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly SectionFetcherSectionTracksLoader sectionTracksLoader;

    public SectionFetcher(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.sectionTracksLoader = new SectionFetcherSectionTracksLoader(httpClient, logger);
    }

    public async Task<bool> FetchPublicationSectionsAsync(FetchPublicationSectionsRequest request)
    {
        var db = request.Db;
        var normalizedPublicationCode = request.NormalizedPublicationCode;
        var normalizedLanguageCode = request.NormalizedLanguageCode;
        var englishPublication = request.EnglishPublication;
        var sectionCodes = request.SectionCodes;
        var cancellationToken = request.CancellationToken;
        var progress = request.Progress;

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
            .Where(s => !string.IsNullOrEmpty(s.SectionCode))
            .Select(s => s.SectionCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var missingSectionCodes = sectionCodes
            .Where(sc => !existingSectionCodes.Contains(sc))
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

        var publication = await GetOrCreatePublicationForSectionFetchAsync(
            db,
            existingPublication,
            normalizedPublicationCode,
            englishPublication,
            language,
            categoriesForPub,
            effectiveToken);

        var (isBible, isIssueSectioned) = ComputeSectionFetchFlags(normalizedPublicationCode, category);

        var localizedPubNameSlot = new LocalizedPublicationNameSlot();
        await ProcessMissingPublicationSectionsAsync(new MissingPublicationSectionsProcessArgs
        {
            Db = db,
            Publication = publication,
            NormalizedPublicationCode = normalizedPublicationCode,
            NormalizedLanguageCode = normalizedLanguageCode,
            MissingSectionCodes = missingSectionCodes,
            IsBible = isBible,
            IsIssueSectioned = isIssueSectioned,
            LocalizedPubNameSlot = localizedPubNameSlot,
            EffectiveToken = effectiveToken,
            Progress = progress
        });

        if (publication.Sections.Count == 0)
        {
            logger.Warning("No sections found for publication {PublicationCode} in language {LanguageCode}",
                normalizedPublicationCode, normalizedLanguageCode);
            db.BiblePublications.Remove(publication);
            await SaveChangesWithRetryAsync(db, effectiveToken);
            return false;
        }

        logger.Information("Successfully fetched {Count} sections for publication {PublicationCode} in language {LanguageCode}",
            publication.Sections.Count, normalizedPublicationCode, normalizedLanguageCode);

        progress?.UpdateProgress(1.0);
        return true;
    }

    private static (bool IsBible, bool IsIssueSectioned) ComputeSectionFetchFlags(
        string normalizedPublicationCode,
        Category category) =>
        (category.CategoryCode.Equals(AppConstants.Media.BiblePublicationCategoryBible, StringComparison.OrdinalIgnoreCase),
            MagazineHelper.IsMagazinePublicationCode(normalizedPublicationCode));

    private async Task<BiblePublication> GetOrCreatePublicationForSectionFetchAsync(
        MediaDbContext db,
        BiblePublication? existingPublication,
        string normalizedPublicationCode,
        BiblePublication englishPublication,
        Language language,
        List<Category> categoriesForPub,
        CancellationToken effectiveToken)
    {
        var determinedCatalogType = PublicationTypeHelper.GetCatalogType(normalizedPublicationCode);
        var isMusicPub = ComputeIsMusicPublication(categoriesForPub, normalizedPublicationCode);

        if (existingPublication != null)
        {
            return PrepareExistingPublicationForSectionFetch(existingPublication, categoriesForPub, isMusicPub, determinedCatalogType);
        }

        return await InsertNewPublicationForSectionFetchAsync(db,
            new InsertNewPublicationForSectionFetchSpec
            {
                NormalizedPublicationCode = normalizedPublicationCode,
                EnglishPublication = englishPublication,
                Language = language,
                CategoriesForPub = categoriesForPub,
                IsMusicPub = isMusicPub,
                DeterminedCatalogType = determinedCatalogType,
                EffectiveToken = effectiveToken
            });
    }

    private sealed class InsertNewPublicationForSectionFetchSpec
    {
        public required string NormalizedPublicationCode { get; init; }
        public required BiblePublication EnglishPublication { get; init; }
        public required Language Language { get; init; }
        public required List<Category> CategoriesForPub { get; init; }
        public required bool IsMusicPub { get; init; }
        public required CatalogType? DeterminedCatalogType { get; init; }
        public required CancellationToken EffectiveToken { get; init; }
    }

    private static bool ComputeIsMusicPublication(List<Category> categoriesForPub, string normalizedPublicationCode) =>
        categoriesForPub.Any(c =>
            c.CategoryCode.Equals(AppConstants.Media.BiblePublicationCategoryMusic,
                StringComparison.OrdinalIgnoreCase)) ||
        JwSourceHelper.MusicFlagPublicationCodes.Contains(normalizedPublicationCode);

    private static BiblePublication PrepareExistingPublicationForSectionFetch(
        BiblePublication existingPublication,
        List<Category> categoriesForPub,
        bool isMusicPub,
        CatalogType? determinedCatalogType)
    {
        existingPublication.IsMusic = isMusicPub;
        if (existingPublication.CatalogType == null)
        {
            existingPublication.CatalogType = determinedCatalogType;
        }

        SyncPublicationCategories(existingPublication, categoriesForPub);
        return existingPublication;
    }

    private async Task<BiblePublication> InsertNewPublicationForSectionFetchAsync(
        MediaDbContext db,
        InsertNewPublicationForSectionFetchSpec spec)
    {
        var normalizedPublicationCode = spec.NormalizedPublicationCode;
        var englishPublication = spec.EnglishPublication;
        var language = spec.Language;
        var categoriesForPub = spec.CategoriesForPub;
        var isMusicPub = spec.IsMusicPub;
        var determinedCatalogType = spec.DeterminedCatalogType;
        var effectiveToken = spec.EffectiveToken;
        var isVideoDrama = PublicationTypeHelper.IsVideo(normalizedPublicationCode);
        var publication = new BiblePublication
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
        return publication;
    }

    private async Task ProcessMissingPublicationSectionsAsync(MissingPublicationSectionsProcessArgs args)
    {
        var db = args.Db;
        var publication = args.Publication;
        var normalizedPublicationCode = args.NormalizedPublicationCode;
        var normalizedLanguageCode = args.NormalizedLanguageCode;
        var missingSectionCodes = args.MissingSectionCodes;
        var isBible = args.IsBible;
        var isIssueSectioned = args.IsIssueSectioned;
        var localizedPubNameSlot = args.LocalizedPubNameSlot;
        var effectiveToken = args.EffectiveToken;
        var progress = args.Progress;

        var totalSections = missingSectionCodes.Count;
        var completedSections = 0;

        foreach (var sectionCode in missingSectionCodes)
        {
            effectiveToken.ThrowIfCancellationRequested();

            var outcome = await ProcessSingleMissingSectionAsync(new MissingSectionFetchRequest
            {
                Db = db,
                Publication = publication,
                NormalizedPublicationCode = normalizedPublicationCode,
                NormalizedLanguageCode = normalizedLanguageCode,
                SectionCode = sectionCode,
                IsBible = isBible,
                IsIssueSectioned = isIssueSectioned,
                LocalizedPubName = localizedPubNameSlot,
                EffectiveToken = effectiveToken
            });

            if (outcome.CompletedDelta > 0)
            {
                completedSections += outcome.CompletedDelta;
                if (outcome.UpdateProgress && totalSections > 0)
                {
                    progress?.UpdateProgress((double)completedSections / totalSections);
                }
            }
        }
    }

    private sealed class MissingPublicationSectionsProcessArgs
    {
        public required MediaDbContext Db { get; init; }
        public required BiblePublication Publication { get; init; }
        public required string NormalizedPublicationCode { get; init; }
        public required string NormalizedLanguageCode { get; init; }
        public required List<string> MissingSectionCodes { get; init; }
        public required bool IsBible { get; init; }
        public required bool IsIssueSectioned { get; init; }
        public required LocalizedPublicationNameSlot LocalizedPubNameSlot { get; init; }
        public required CancellationToken EffectiveToken { get; init; }
        public IFetchProgress? Progress { get; init; }
    }

    private readonly record struct MissingSectionProcessOutcome(int CompletedDelta, bool UpdateProgress);

    private sealed class MissingSectionFetchRequest
    {
        public required MediaDbContext Db { get; init; }
        public required BiblePublication Publication { get; init; }
        public required string NormalizedPublicationCode { get; init; }
        public required string NormalizedLanguageCode { get; init; }
        public required string SectionCode { get; init; }
        public required bool IsBible { get; init; }
        public required bool IsIssueSectioned { get; init; }
        public required LocalizedPublicationNameSlot LocalizedPubName { get; init; }
        public required CancellationToken EffectiveToken { get; init; }
    }

    private async Task<MissingSectionProcessOutcome> ProcessSingleMissingSectionAsync(MissingSectionFetchRequest request)
    {
        try
        {
            var iteration = await TryFetchAndPersistSingleMissingSectionAsync(request);

            if (iteration.IncrementCompleted)
            {
                return new MissingSectionProcessOutcome(1, iteration.UpdateProgressThisIteration);
            }

            return default;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code", StringComparison.Ordinal))
        {
            logger.Debug(ex,
                "Section {SectionCode} not available for publication {PublicationCode} in language {LanguageCode}",
                request.SectionCode, request.NormalizedPublicationCode, request.NormalizedLanguageCode);
            return new MissingSectionProcessOutcome(1, false);
        }
        catch (Exception ex)
        {
            if (NetworkExceptionHelper.IsNetworkFailure(ex))
            {
                throw;
            }

            logger.Warning(ex,
                "Failed to fetch section {SectionCode} for publication {PublicationCode} in language {LanguageCode}",
                request.SectionCode, request.NormalizedPublicationCode, request.NormalizedLanguageCode);
            return new MissingSectionProcessOutcome(1, false);
        }
    }

    private sealed class LocalizedPublicationNameSlot
    {
        public string? Value;
    }

    private readonly record struct MissingSectionIteration(bool IncrementCompleted, bool UpdateProgressThisIteration);

    private async Task<MissingSectionIteration> TryFetchAndPersistSingleMissingSectionAsync(MissingSectionFetchRequest request)
    {
        var dramaFileFormat = !request.IsBible && !request.IsIssueSectioned && PublicationTypeHelper.IsVideo(request.NormalizedPublicationCode)
            ? AppConstants.Media.MediaStreamFormatMp4
            : AppConstants.Media.MediaStreamFormatMp3;

        var queryString = BuildMissingSectionQueryString(
            request.NormalizedPublicationCode,
            request.NormalizedLanguageCode,
            request.SectionCode,
            request.IsIssueSectioned,
            request.IsBible,
            dramaFileFormat);

        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
        var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, request.EffectiveToken);
        if (jsonString == null)
        {
            logger.Debug("Section {SectionCode} not available for publication {PublicationCode} in language {LanguageCode}",
                request.SectionCode, request.NormalizedPublicationCode, request.NormalizedLanguageCode);
            return new MissingSectionIteration(true, false);
        }

        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement))
        {
            return new MissingSectionIteration(true, false);
        }

        var sectionName = ResolveSectionDisplayNameFromPubMediaJson(root, request.IsIssueSectioned);
        if (sectionName == null)
        {
            logger.Debug("Section name not found in API response for section {SectionCode} in language {LanguageCode}",
                request.SectionCode, request.NormalizedLanguageCode);
        }

        TryApplyLocalizedPublicationNameFromPubMediaJson(
            root,
            request.Publication,
            request.LocalizedPubName,
            request.IsIssueSectioned,
            request.IsBible,
            request.NormalizedPublicationCode,
            request.NormalizedLanguageCode);

        var tracks = ParsePublicationSectionTracksFromFiles(
            filesElement,
            request.IsIssueSectioned,
            request.IsBible,
            request.NormalizedPublicationCode,
            request.NormalizedLanguageCode,
            request.SectionCode);

        var section = new BiblePublicationSection
        {
            Name = sectionName ?? request.SectionCode,
            SectionCode = request.SectionCode.ToLowerInvariant(),
            BiblePublication = request.Publication,
            Tracks = new List<BiblePublicationTrack>()
        };

        request.Publication.Sections.Add(section);
        await SaveChangesWithRetryAsync(request.Db, request.EffectiveToken);

        foreach (var track in tracks)
        {
            track.Section = section;
            track.Publication = request.Publication;
        }

        section.Tracks.AddRange(tracks);
        await SaveChangesWithRetryAsync(request.Db, request.EffectiveToken);

        return new MissingSectionIteration(true, true);
    }

    private static string BuildMissingSectionQueryString(
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        string sectionCode,
        bool isIssueSectioned,
        bool isBible,
        string dramaFileFormat)
    {
        if (isIssueSectioned)
        {
            var (apiPubCode, issueCode) = MagazineHelper.ParseSectionCode(sectionCode);
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={apiPubCode}&{AppConstants.Media.GetPubQueryParamName.Issue}={issueCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
        }

        if (isBible)
        {
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={normalizedPublicationCode}&{AppConstants.Media.GetPubQueryParamName.BookNum}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
        }

        return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={dramaFileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
    }

    private static string? ResolveSectionDisplayNameFromPubMediaJson(JsonElement root, bool isIssueSectioned)
    {
        if (isIssueSectioned)
        {
            string? pubName = null;
            string? formattedDate = null;
            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pnEl))
            {
                pubName = pnEl.GetString();
            }

            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.FormattedDate, out var fdEl))
            {
                formattedDate = fdEl.GetString();
            }

            return MagazineHelper.BuildSectionName(pubName, formattedDate);
        }

        if (root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pubNameElement))
        {
            var rawName = pubNameElement.GetString();
            return MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
        }

        return null;
    }

    private void TryApplyLocalizedPublicationNameFromPubMediaJson(
        JsonElement root,
        BiblePublication publication,
        LocalizedPublicationNameSlot localizedPubName,
        bool isIssueSectioned,
        bool isBible,
        string normalizedPublicationCode,
        string normalizedLanguageCode)
    {
        if (isIssueSectioned && localizedPubName.Value == null)
        {
            localizedPubName.Value = MagazineHelper.GetYear(normalizedPublicationCode).ToString();
            publication.Name = localizedPubName.Value;
            return;
        }

        if (localizedPubName.Value != null || !root.TryGetProperty(AppConstants.Media.PubMediaJson.ParentPubName, out var parentPubNameElement))
        {
            return;
        }

        var rawName = parentPubNameElement.GetString();
        var extractedName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);

        if (!string.IsNullOrEmpty(extractedName) && isBible)
        {
            var isVideoName = AppConstants.Media.ApiMisleadingGoodNewsVideoPublicationNamePhrases.Any(vn =>
                extractedName!.Contains(vn, StringComparison.OrdinalIgnoreCase));

            if (isVideoName)
            {
                logger.Warning("API returned video/drama publication name '{ExtractedName}' for Bible publication {PublicationCode} in language {LanguageCode}. This is likely an API error. Skipping this name and using fallback.",
                    extractedName, normalizedPublicationCode, normalizedLanguageCode);
                extractedName = null;
            }
        }

        if (!string.IsNullOrEmpty(extractedName))
        {
            localizedPubName.Value = extractedName;
            publication.Name = localizedPubName.Value;
        }
    }

    private static List<BiblePublicationTrack> ParsePublicationSectionTracksFromFiles(
        JsonElement filesElement,
        bool isIssueSectioned,
        bool isBible,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        string sectionCode)
    {
        if (isIssueSectioned)
        {
            return EnglishTrackParser.ParseGenericTracks(
                filesElement, normalizedLanguageCode, AppConstants.Media.MediaStreamFormatMp3);
        }

        if (isBible)
        {
            return EnglishTrackParser.ParseBibleTracks(
                filesElement, normalizedLanguageCode);
        }

        var isVideoDrama = PublicationTypeHelper.IsVideo(normalizedPublicationCode);
        return MediatorTrackParser.ParseTracksFromJson(
            filesElement,
            new MediatorTrackParseContext(normalizedLanguageCode, sectionCode, IsVideo: isVideoDrama));
    }

    public Task<bool> FetchSectionTracksAsync(FetchSectionTracksRequest request)
    {
        return sectionTracksLoader.FetchSectionTracksAsync(request);
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
                if (SectionFetcherSqliteExceptionHelper.IsBusyOrLocked(ex))
                {
                    logger.Debug(ex, "SaveChanges failed with database locked (attempt {Attempt}/{Max}), retrying",
                        attempt, maxAttempts);
                    await Task.Delay(100 * attempt, cancellationToken);
                    continue;
                }
                throw;
            }
        }
    }

    private static void SyncPublicationCategories(BiblePublication publication, List<Category> categories)
    {
        var existingCategoryIds = publication.BiblePublicationCategories
            .Select(bpc => bpc.CategoryId)
            .ToHashSet();
        foreach (var cat in categories.Where(c => existingCategoryIds.Add(c.Id)))
        {
            publication.BiblePublicationCategories.Add(
                new BiblePublicationCategory { BiblePublicationId = publication.Id, CategoryId = cat.Id, Category = cat });
        }
    }
}
