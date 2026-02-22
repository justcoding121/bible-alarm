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
        IFetchProgress? progress = null)
    {
        // Step 1: Get all available publication codes from PublicationLanguages (discovery table)
        // This shows all publications that are available for this language/category, even if not yet downloaded
        var availablePublicationCodes = await biblePublicationService.GetAvailablePublicationCodesAsync(
            languageCode, categoryName, cancellationToken);

        Log.Debug(
            "GetBiblePublications: Found {Count} available publication codes from PublicationLanguages for language={LanguageCode}, category={CategoryName}",
            availablePublicationCodes.Count,
            languageCode,
            categoryName ?? "all");

        // Step 2: Get downloaded publications from BiblePublications table
        // Include both publications with the selected language AND publications without language FK (LanguageId == null)
        var downloadedPublications = await biblePublicationService.GetByLanguageCodeAsync(
            languageCode, categoryName, cancellationToken);

        // Also get publications without language FK (LanguageId == null) for this category
        // This is data-driven - works for any category that has publications without language
        Dictionary<string, BiblePublication> publicationsWithoutLanguage = new();
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var query = db.BiblePublications
                .AsNoTracking()
                .Include(x => x.BiblePublicationCategories)
                .ThenInclude(x => x.Category)
                .Where(x => x.LanguageId == null);

            // Filter by category if provided (categoryName is CategoryCode)
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == categoryName));
            }

            var pubsWithoutLang = await query.ToListAsync(cancellationToken);
            foreach (var pub in pubsWithoutLang)
            {
                publicationsWithoutLanguage[pub.PublicationCode] = pub;
            }
        }

        Log.Debug(
            "GetBiblePublications: Found {Count} downloaded publications for language={LanguageCode}, category={CategoryName}, and {CountWithoutLang} publications without language FK",
            downloadedPublications.Count,
            languageCode,
            categoryName ?? "all",
            publicationsWithoutLanguage.Count);

        // Step 3: Merge - use downloaded publications where available, create placeholders for others
        var result = new Dictionary<string, BiblePublication>();

        // Add downloaded publications with language
        foreach (var downloadedPub in downloadedPublications.Values)
        {
            result[downloadedPub.PublicationCode] = downloadedPub;
        }

        // Add downloaded publications without language FK (data-driven, not hard-coded)
        foreach (var pubWithoutLang in publicationsWithoutLanguage.Values)
        {
            var code = pubWithoutLang.PublicationCode;
            if (result.ContainsKey(code))
            {
                continue;
            }

            result[code] = pubWithoutLang;
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
            Log.Debug("GetBiblePublications: Creating placeholders for {Count} publications not yet downloaded", missingPublicationCodes.Count);

            // Get Category and Language info from PublicationLanguages for missing publications with LanguageId
            // Use case-sensitive codes: "Dramas", "DramaticBibleReadings", "gnj" (preserve exact case)
            var publicationLanguageInfo = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Category)
                .Include(pl => pl.Language)
                .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode)
                .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryCode == categoryName))
                .ToListAsync(cancellationToken);

            // Also get PublicationLanguages entries with LanguageId == null (publications without language)
            // These should always be included regardless of the selected language
            var publicationLanguagesWithoutLanguage = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Category)
                .Where(pl => pl.LanguageId == null)
                .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryCode == categoryName))
                .ToListAsync(cancellationToken);

            // Group by normalized publication code to handle duplicates (case-insensitive comparison, preserve case)
            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAddPlaceholder(PublicationLanguage plInfo, string codeForKey)
            {
                if (plInfo.Category == null || !missingPublicationCodes.Any(c => string.Equals(c, codeForKey, StringComparison.OrdinalIgnoreCase)) || seenCodes.Contains(codeForKey))
                {
                    return;
                }

                seenCodes.Add(codeForKey);
                var isMusic = plInfo.Category?.CategoryCode?.Equals("Music", StringComparison.OrdinalIgnoreCase) == true;
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
                        ? "Dramas"
                        : "DramaticBibleReadings";
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
                        ? "Dramas"
                        : "DramaticBibleReadings";
                }
                if (!result.ContainsKey(codeForKey))
                {
                    TryAddPlaceholder(plInfo, codeForKey);
                }
            }
        }

        // Also ensure publications without LanguageId from PublicationLanguages are always included
        // (even if they're not in availablePublicationCodes, they should still be shown)
        var publicationLanguagesWithoutLanguageForPlaceholders = await dbContext.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Category)
            .Where(pl => pl.LanguageId == null)
            .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryCode == categoryName))
            .ToListAsync(cancellationToken);

        foreach (var plInfo in publicationLanguagesWithoutLanguageForPlaceholders)
        {
            // Normalize drama codes to case-sensitive format, preserve exact case for others
            var codeForKey = plInfo.PublicationCode;
            var lowerCode = codeForKey.ToLowerInvariant();
            if (PublicationTypeHelper.IsDrama(lowerCode))
            {
                codeForKey = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }

            // Only add if not already in result (avoid duplicates)
            if (!result.ContainsKey(codeForKey) && plInfo.Category != null)
            {
                var isMusic = plInfo.Category.CategoryCode.Equals("Music", StringComparison.OrdinalIgnoreCase);
                var placeholder = new BiblePublication
                {
                    Id = 0, // Not saved yet
                    PublicationCode = codeForKey,
                    Name = codeForKey, // Placeholder name - will be updated when downloaded
                    BiblePublicationCategories = new List<BiblePublicationCategory> { new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = plInfo.CategoryId, Category = plInfo.Category } },
                    LanguageId = null, // No language for these publications
                    Language = null,
                    Sections = new List<BiblePublicationSection>(),
                    Tracks = new List<BiblePublicationTrack>(),
                    IsVideo = false,
                    IsMusic = isMusic
                };

                result[placeholder.PublicationCode] = placeholder;
            }
        }

        Log.Information(
            "GetBiblePublications: Returning {TotalCount} publications ({DownloadedCount} downloaded, {PlaceholderCount} placeholders) for language={LanguageCode}, category={CategoryName}",
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
            // Show progress if callback provided
            progress?.SetIsVisible(true);
            progress?.UpdateProgress(0.0);

            try
            {
                Log.Information(
                    "Ensuring all publications are downloaded for language {LanguageCode} (publication modal opened)",
                    languageCode);
                await languageContentService.EnsureAllPublicationsForLanguageAsync(
                    languageCode,
                    categoryName,
                    cancellationToken,
                    progress);

                // IMPORTANT:
                // Step 3 created placeholders using PublicationCode as the display name.
                // After EnsureAllPublicationsForLanguageAsync runs, those publications may now exist in BiblePublications with localized names.
                // Refresh downloaded publications and overwrite placeholders so UI can display localized names immediately.
                var refreshedDownloadedPublications =
                    await biblePublicationService.GetByLanguageCodeAsync(languageCode, categoryName, cancellationToken);

                foreach (var refreshed in refreshedDownloadedPublications.Values)
                {
                    result[refreshed.PublicationCode] = refreshed;
                }

                Log.Information(
                    "GetBiblePublications: Refreshed {Count} downloaded publications after ensuring all publications for language={LanguageCode}, category={CategoryName}",
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
                Log.Warning(ex, "Failed to ensure all publications for language {LanguageCode}", languageCode);
            }
            finally
            {
                progress?.SetIsVisible(false);
            }
        }

        return result;
    }
}

