#nullable enable annotations

using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Media;

internal sealed record GetBiblePublicationsServices(
    IBiblePublicationService BiblePublicationService,
    ILanguageContentService LanguageContentService,
    IServiceScopeFactory ScopeFactory);

internal sealed record GetBiblePublicationsOptions(
    string LanguageCode,
    string? CategoryName = null,
    bool DownloadAll = false,
    IFetchProgress? Progress = null,
    bool RequireIsMusicForMusicCategory = false,
    CancellationToken CancellationToken = default);

internal static class MediaServiceBiblePublicationList
{
    internal static async Task<Dictionary<string, BiblePublication>> GetBiblePublicationsAsync(
        GetBiblePublicationsServices services,
        GetBiblePublicationsOptions options)
    {
        var biblePublicationService = services.BiblePublicationService;
        var scopeFactory = services.ScopeFactory;
        var languageCode = options.LanguageCode;
        var categoryName = options.CategoryName;
        var downloadAll = options.DownloadAll;
        var progress = options.Progress;
        var requireIsMusicForMusicCategory = options.RequireIsMusicForMusicCategory;
        var cancellationToken = options.CancellationToken;

        // Step 1: Get all available publication codes from PublicationLanguages (discovery table)
        var availablePublicationCodes = await biblePublicationService.GetAvailablePublicationCodesAsync(
            languageCode, categoryName, requireIsMusicForMusicCategory, cancellationToken);

        Log.Debug(AppConstants.Logging.MediaServiceDiagnosticsLog.GetBiblePublicationsAvailableCodesFromPublicationLanguages,
            availablePublicationCodes.Count,
            languageCode,
            categoryName ?? "all");

        // Step 2: Get downloaded publications from BiblePublications table
        var downloadedPublications = await biblePublicationService.GetByLanguageCodeAsync(
            languageCode, categoryName, requireIsMusicForMusicCategory, cancellationToken);

        var publicationsWithoutLanguage = await LoadPublicationsWithoutLanguageAsync(
            scopeFactory,
            categoryName,
            requireIsMusicForMusicCategory,
            cancellationToken);

        Log.Debug(AppConstants.Logging.MediaServiceDiagnosticsLog.GetBiblePublicationsDownloadedCountSummary,
            downloadedPublications.Count,
            languageCode,
            categoryName ?? "all",
            publicationsWithoutLanguage.Count);

        // Step 3: Merge - use downloaded publications where available, add non-language pubs, then placeholders for the rest
        var result = new Dictionary<string, BiblePublication>(downloadedPublications, StringComparer.OrdinalIgnoreCase);

        foreach (var pubWithoutLang in publicationsWithoutLanguage.Values.Where(p => !result.ContainsKey(p.PublicationCode)))
        {
            result[pubWithoutLang.PublicationCode] = pubWithoutLang;
        }

        using var scope2 = scopeFactory.CreateScope();
        var dbContext = scope2.ServiceProvider.GetRequiredService<MediaDbContext>();

        await MergePlaceholderPublicationsAsync(
            dbContext,
            languageCode,
            categoryName,
            requireIsMusicForMusicCategory,
            availablePublicationCodes,
            result,
            cancellationToken);

        Log.Information(AppConstants.Logging.MediaServiceDiagnosticsLog.GetBiblePublicationsReturningTotalCounts,
            result.Count,
            downloadedPublications.Count,
            result.Count - downloadedPublications.Count,
            languageCode,
            categoryName ?? "all");

        await TryEnsureAllPublicationsForLanguageModalAsync(new EnsureAllPublicationsForLanguageModalArgs
        {
            Services = services,
            LanguageCode = languageCode,
            CategoryName = categoryName,
            DownloadAll = downloadAll,
            Progress = progress,
            RequireIsMusicForMusicCategory = requireIsMusicForMusicCategory,
            Result = result,
            CancellationToken = cancellationToken
        });

        return result;
    }

    private static async Task<Dictionary<string, BiblePublication>> LoadPublicationsWithoutLanguageAsync(
        IServiceScopeFactory scopeFactory,
        string? categoryName,
        bool requireIsMusicForMusicCategory,
        CancellationToken cancellationToken)
    {
        Dictionary<string, BiblePublication> publicationsWithoutLanguage = new(StringComparer.OrdinalIgnoreCase);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var query = db.BiblePublications
            .AsNoTracking()
            .Include(x => x.BiblePublicationCategories)
            .ThenInclude(x => x.Category)
            .Where(x => x.LanguageId == null);

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            query = query.Where(x => x.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == categoryName));
            if (MusicCategoryPublicationQueryGate.AppliesMusicOnlyFilter(categoryName, requireIsMusicForMusicCategory))
            {
                query = query.Where(x => x.IsMusic);
            }
        }

        var pubsWithoutLang = await query.ToListAsync(cancellationToken);
        foreach (var pub in pubsWithoutLang)
        {
            publicationsWithoutLanguage[pub.PublicationCode] = pub;
        }

        return publicationsWithoutLanguage;
    }

    private static IQueryable<PublicationLanguage> WhereOptionalCategoryMatches(
        IQueryable<PublicationLanguage> query,
        string? categoryName) =>
        query.Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryCode == categoryName));

    private static IQueryable<PublicationLanguage> WhereMusicRequiredIfApplicable(
        IQueryable<PublicationLanguage> query,
        string? categoryName,
        bool requireIsMusicForMusicCategory)
    {
        if (MusicCategoryPublicationQueryGate.AppliesMusicOnlyFilter(categoryName, requireIsMusicForMusicCategory))
        {
            return query.Where(pl => pl.IsMusic);
        }

        return query;
    }

    private static async Task MergePlaceholderPublicationsAsync(
        MediaDbContext dbContext,
        string languageCode,
        string? categoryName,
        bool requireIsMusicForMusicCategory,
        IReadOnlyCollection<string> availablePublicationCodes,
        Dictionary<string, BiblePublication> result,
        CancellationToken cancellationToken)
    {
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var missingPublicationCodes = availablePublicationCodes
            .Where(code => !result.ContainsKey(code))
            .ToList();

        if (missingPublicationCodes.Count > 0)
        {
            await MergeMissingPublicationCodePlaceholdersAsync(
                dbContext,
                normalizedLanguageCode,
                categoryName,
                requireIsMusicForMusicCategory,
                missingPublicationCodes,
                result,
                cancellationToken);
        }

        await MergeLanguageNullPublicationPlaceholdersAsync(
            dbContext,
            categoryName,
            requireIsMusicForMusicCategory,
            result,
            cancellationToken);
    }

    private static async Task MergeMissingPublicationCodePlaceholdersAsync(
        MediaDbContext dbContext,
        string normalizedLanguageCode,
        string? categoryName,
        bool requireIsMusicForMusicCategory,
        List<string> missingPublicationCodes,
        Dictionary<string, BiblePublication> result,
        CancellationToken cancellationToken)
    {
        Log.Debug(AppConstants.Logging.MediaServiceDiagnosticsLog.GetBiblePublicationsCreatingPlaceholders, missingPublicationCodes.Count);

        var publicationLanguageInfoQuery = WhereMusicRequiredIfApplicable(
            WhereOptionalCategoryMatches(
                dbContext.PublicationLanguages
                    .AsNoTracking()
                    .Include(pl => pl.Category)
                    .Include(pl => pl.Language)
                    .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode),
                categoryName),
            categoryName,
            requireIsMusicForMusicCategory);

        var publicationLanguageInfo = await publicationLanguageInfoQuery.ToListAsync(cancellationToken);

        var publicationLanguagesWithoutLanguageQuery = WhereMusicRequiredIfApplicable(
            WhereOptionalCategoryMatches(
                dbContext.PublicationLanguages
                    .AsNoTracking()
                    .Include(pl => pl.Category)
                    .Where(pl => pl.LanguageId == null),
                categoryName),
            categoryName,
            requireIsMusicForMusicCategory);

        var publicationLanguagesWithoutLanguage = await publicationLanguagesWithoutLanguageQuery.ToListAsync(cancellationToken);

        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var plInfo in publicationLanguageInfo)
        {
            TryAddPlaceholderForMissingCode(
                plInfo,
                PublicationTypeHelper.GetCanonicalPublicationCodeForDatabase(plInfo.PublicationCode),
                missingPublicationCodes,
                seenCodes,
                result);
        }

        foreach (var plInfo in publicationLanguagesWithoutLanguage)
        {
            var codeForKey = PublicationTypeHelper.GetCanonicalPublicationCodeForDatabase(plInfo.PublicationCode);
            if (!result.ContainsKey(codeForKey))
            {
                TryAddPlaceholderForMissingCode(plInfo, codeForKey, missingPublicationCodes, seenCodes, result);
            }
        }
    }

    private static void TryAddPlaceholderForMissingCode(
        PublicationLanguage plInfo,
        string codeForKey,
        IReadOnlyCollection<string> missingPublicationCodes,
        HashSet<string> seenCodes,
        Dictionary<string, BiblePublication> result)
    {
        if (plInfo.Category == null ||
            !missingPublicationCodes.Any(c => string.Equals(c, codeForKey, StringComparison.OrdinalIgnoreCase)) ||
            seenCodes.Contains(codeForKey))
        {
            return;
        }

        seenCodes.Add(codeForKey);
        var isMusic = plInfo.Category?.CategoryCode?.Equals(AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase) is true;
        var placeholderName = JwSourceHelper.GetPublicationDisplayNameFallback(codeForKey) ?? codeForKey;
        var placeholder = new BiblePublication
        {
            Id = 0,
            PublicationCode = codeForKey,
            Name = placeholderName,
            BiblePublicationCategories = new List<BiblePublicationCategory> { new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = plInfo.CategoryId, Category = plInfo.Category } },
            LanguageId = plInfo.LanguageId,
            Language = plInfo.Language,
            Sections = new List<BiblePublicationSection>(),
            Tracks = new List<BiblePublicationTrack>(),
            IsVideo = false,
            IsMusic = isMusic
        };
        result[placeholder.PublicationCode] = placeholder;
    }

    private static async Task MergeLanguageNullPublicationPlaceholdersAsync(
        MediaDbContext dbContext,
        string? categoryName,
        bool requireIsMusicForMusicCategory,
        Dictionary<string, BiblePublication> result,
        CancellationToken cancellationToken)
    {
        var publicationLanguagesWithoutLanguageForPlaceholdersQuery = WhereMusicRequiredIfApplicable(
            WhereOptionalCategoryMatches(
                dbContext.PublicationLanguages
                    .AsNoTracking()
                    .Include(pl => pl.Category)
                    .Where(pl => pl.LanguageId == null),
                categoryName),
            categoryName,
            requireIsMusicForMusicCategory);

        var publicationLanguagesWithoutLanguageForPlaceholders =
            await publicationLanguagesWithoutLanguageForPlaceholdersQuery.ToListAsync(cancellationToken);

        foreach (var plInfo in publicationLanguagesWithoutLanguageForPlaceholders)
        {
            var codeForKey = PublicationTypeHelper.GetCanonicalPublicationCodeForDatabase(plInfo.PublicationCode);

            if (!result.ContainsKey(codeForKey) && plInfo.Category != null)
            {
                var isMusic = plInfo.Category.CategoryCode.Equals(AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase);
                var placeholder = new BiblePublication
                {
                    Id = 0,
                    PublicationCode = codeForKey,
                    Name = codeForKey,
                    BiblePublicationCategories = new List<BiblePublicationCategory> { new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = plInfo.CategoryId, Category = plInfo.Category } },
                    LanguageId = null,
                    Language = null,
                    Sections = new List<BiblePublicationSection>(),
                    Tracks = new List<BiblePublicationTrack>(),
                    IsVideo = false,
                    IsMusic = isMusic
                };

                result[placeholder.PublicationCode] = placeholder;
            }
        }
    }

    private static async Task TryEnsureAllPublicationsForLanguageModalAsync(EnsureAllPublicationsForLanguageModalArgs args)
    {
        var services = args.Services;
        var languageCode = args.LanguageCode;
        var categoryName = args.CategoryName;
        var downloadAll = args.DownloadAll;
        var progress = args.Progress;
        var requireIsMusicForMusicCategory = args.RequireIsMusicForMusicCategory;
        var result = args.Result;
        var cancellationToken = args.CancellationToken;

        var languageContentService = services.LanguageContentService;
        var biblePublicationService = services.BiblePublicationService;

        if (!downloadAll || string.IsNullOrEmpty(languageCode) || languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Log.Information(AppConstants.Logging.MediaServiceDiagnosticsLog.EnsuringAllPublicationsDownloadedPublicationModalOpened,
                languageCode);
            await languageContentService.EnsureAllPublicationsForLanguageAsync(
                languageCode,
                categoryName,
                progress,
                cancellationToken);

            var refreshedDownloadedPublications =
                await biblePublicationService.GetByLanguageCodeAsync(languageCode, categoryName, requireIsMusicForMusicCategory, cancellationToken);

            foreach (var refreshed in refreshedDownloadedPublications.Values)
            {
                result[refreshed.PublicationCode] = refreshed;
            }

            Log.Information(AppConstants.Logging.MediaServiceDiagnosticsLog.GetBiblePublicationsRefreshedAfterEnsuring,
                refreshedDownloadedPublications.Count,
                languageCode,
                categoryName ?? "all");
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (SocketException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.MediaServiceDiagnosticsLog.FailedEnsureAllPublicationsForLanguage, languageCode);
        }
    }

    private sealed class EnsureAllPublicationsForLanguageModalArgs
    {
        public required GetBiblePublicationsServices Services { get; init; }
        public required string LanguageCode { get; init; }
        public string? CategoryName { get; init; }
        public required bool DownloadAll { get; init; }
        public IFetchProgress? Progress { get; init; }
        public required bool RequireIsMusicForMusicCategory { get; init; }
        public required Dictionary<string, BiblePublication> Result { get; init; }
        public required CancellationToken CancellationToken { get; init; }
    }
}

