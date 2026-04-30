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

internal static class MediaServiceBiblePublicationList
{
    internal static async Task<Dictionary<string, BiblePublication>> GetBiblePublicationsAsync(
        IBiblePublicationService biblePublicationService,
        ILanguageContentService languageContentService,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken,
        string languageCode,
        string? categoryName = null,
        bool downloadAll = false,
        IFetchProgress? progress = null,
        bool requireIsMusicForMusicCategory = false)
    {
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

        // Include non-language publications (LanguageId == null) for the category in every language.
        Dictionary<string, BiblePublication> publicationsWithoutLanguage = new(StringComparer.OrdinalIgnoreCase);
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var query = db.BiblePublications
                .AsNoTracking()
                .Include(x => x.BiblePublicationCategories)
                .ThenInclude(x => x.Category)
                .Where(x => x.LanguageId == null);

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == categoryName));
                if (requireIsMusicForMusicCategory && string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.IsMusic);
                }
            }

            var pubsWithoutLang = await query.ToListAsync(cancellationToken);
            foreach (var pub in pubsWithoutLang)
            {
                publicationsWithoutLanguage[pub.PublicationCode] = pub;
            }
        }

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

        // Create placeholders for publications that are available but not yet downloaded
        // We need to get Category and Language from PublicationLanguages to create proper placeholders
        using var scope2 = scopeFactory.CreateScope();
        var dbContext = scope2.ServiceProvider.GetRequiredService<MediaDbContext>();

        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var missingPublicationCodes = availablePublicationCodes
            .Where(code => !result.ContainsKey(code))
            .ToList();

        if (missingPublicationCodes.Count > 0)
        {
            Log.Debug(AppConstants.Logging.MediaServiceDiagnosticsLog.GetBiblePublicationsCreatingPlaceholders, missingPublicationCodes.Count);

            // Get Category and Language info from PublicationLanguages: current language + non-languaged.
            var publicationLanguageInfoQuery = dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Category)
                .Include(pl => pl.Language)
                .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode)
                .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryCode == categoryName));
            if (requireIsMusicForMusicCategory && string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase))
            {
                publicationLanguageInfoQuery = publicationLanguageInfoQuery.Where(pl => pl.IsMusic);
            }
            var publicationLanguageInfo = await publicationLanguageInfoQuery.ToListAsync(cancellationToken);

            var publicationLanguagesWithoutLanguageQuery = dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Category)
                .Where(pl => pl.LanguageId == null)
                .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryCode == categoryName));
            if (requireIsMusicForMusicCategory && string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase))
            {
                publicationLanguagesWithoutLanguageQuery = publicationLanguagesWithoutLanguageQuery.Where(pl => pl.IsMusic);
            }
            var publicationLanguagesWithoutLanguage = await publicationLanguagesWithoutLanguageQuery.ToListAsync(cancellationToken);

            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAddPlaceholder(PublicationLanguage plInfo, string codeForKey)
            {
                if (plInfo.Category == null || !missingPublicationCodes.Any(c => string.Equals(c, codeForKey, StringComparison.OrdinalIgnoreCase)) || seenCodes.Contains(codeForKey))
                    return;

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

            foreach (var plInfo in publicationLanguageInfo)
            {
                var codeForKey = plInfo.PublicationCode;
                var lowerCode = codeForKey.ToLowerInvariant();
                if (PublicationTypeHelper.IsDrama(lowerCode))
                {
                    codeForKey = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                        ? AppConstants.Media.BiblePublicationCategoryDramas
                        : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;
                }
                TryAddPlaceholder(plInfo, codeForKey);
            }

            foreach (var plInfo in publicationLanguagesWithoutLanguage)
            {
                var codeForKey = plInfo.PublicationCode;
                var lowerCode = codeForKey.ToLowerInvariant();
                if (PublicationTypeHelper.IsDrama(lowerCode))
                {
                    codeForKey = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                        ? AppConstants.Media.BiblePublicationCategoryDramas
                        : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;
                }
                if (!result.ContainsKey(codeForKey))
                    TryAddPlaceholder(plInfo, codeForKey);
            }
        }

        var publicationLanguagesWithoutLanguageForPlaceholdersQuery = dbContext.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Category)
            .Where(pl => pl.LanguageId == null)
            .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryCode == categoryName));
        if (requireIsMusicForMusicCategory && string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase))
        {
            publicationLanguagesWithoutLanguageForPlaceholdersQuery = publicationLanguagesWithoutLanguageForPlaceholdersQuery.Where(pl => pl.IsMusic);
        }
        var publicationLanguagesWithoutLanguageForPlaceholders = await publicationLanguagesWithoutLanguageForPlaceholdersQuery.ToListAsync(cancellationToken);

        foreach (var plInfo in publicationLanguagesWithoutLanguageForPlaceholders)
        {
            var codeForKey = plInfo.PublicationCode;
            var lowerCode = codeForKey.ToLowerInvariant();
            if (PublicationTypeHelper.IsDrama(lowerCode))
            {
                codeForKey = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? AppConstants.Media.BiblePublicationCategoryDramas
                    : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;
            }

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

        Log.Information(AppConstants.Logging.MediaServiceDiagnosticsLog.GetBiblePublicationsReturningTotalCounts,
            result.Count,
            downloadedPublications.Count,
            result.Count - downloadedPublications.Count,
            languageCode,
            categoryName ?? "all");

        // Step 4: Download publications based on downloadAll flag
        // - If downloadAll=true (publication modal opened): download all publications with their first sections and tracks
        // - If downloadAll=false (language selected): don't download here (will be done in cascade)
        if (downloadAll && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Log.Information(AppConstants.Logging.MediaServiceDiagnosticsLog.EnsuringAllPublicationsDownloadedPublicationModalOpened,
                    languageCode);
                await languageContentService.EnsureAllPublicationsForLanguageAsync(
                    languageCode,
                    categoryName,
                    progress,
                    cancellationToken);

                // IMPORTANT:
                // Step 3 created placeholders using PublicationCode as the display name.
                // After EnsureAllPublicationsForLanguageAsync runs, those publications may now exist in BiblePublications with localized names.
                // Refresh downloaded publications and overwrite placeholders so UI can display localized names immediately.
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

        return result;
    }
}

