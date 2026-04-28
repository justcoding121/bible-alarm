#nullable enable
using System.Linq;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Media.MediaServiceHelpers;

/// <summary>
/// Builds vocal music releases list (downloaded + placeholders) for a language.
/// </summary>
public static class MediaServiceVocalMusicHelper
{
    public static async Task<Dictionary<string, VocalMusic>> GetReleasesAsync(
        IBiblePublicationService biblePublicationService,
        IVocalMusicService vocalMusicService,
        IServiceScopeFactory scopeFactory,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var availablePublicationCodes = await biblePublicationService.GetAvailablePublicationCodesAsync(
            languageCode, "Music", true, cancellationToken);

        Log.Debug("GetVocalMusicReleases: Found {Count} available publication codes for language={LanguageCode}",
            availablePublicationCodes.Count, languageCode);

        var downloadedReleases = await vocalMusicService.GetByLanguageCodeAsync(languageCode, cancellationToken);

        Dictionary<string, BiblePublication> publicationsWithoutLanguage = new();
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var pubsWithoutLang = await db.BiblePublications
                .AsNoTracking()
                .Include(x => x.BiblePublicationCategories)
                .ThenInclude(x => x.Category)
                .Where(x => x.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == "Music") &&
                           x.LanguageId == null &&
                           x.IsMusic)
                .ToListAsync(cancellationToken);

            foreach (var pub in pubsWithoutLang)
            {
                publicationsWithoutLanguage[pub.PublicationCode] = pub;
            }
        }

        Log.Debug("GetVocalMusicReleases: Found {Count} downloaded for language={LanguageCode}, {CountWithoutLang} without language FK",
            downloadedReleases.Count, languageCode, publicationsWithoutLanguage.Count);

        var result = new Dictionary<string, VocalMusic>();
        foreach (var downloadedRelease in downloadedReleases.Values)
        {
            result[downloadedRelease.Code] = downloadedRelease;
        }
        foreach (var pubWithoutLang in publicationsWithoutLanguage.Values.Where(p => !result.ContainsKey(p.PublicationCode)))
        {
            var vocalMusic = new VocalMusic { Publication = pubWithoutLang };
            result[vocalMusic.Code] = vocalMusic;
        }

        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var missingPublicationCodes = availablePublicationCodes.Where(code => !result.ContainsKey(code)).ToList();

        if (missingPublicationCodes.Count > 0)
        {
            Log.Debug("GetVocalMusicReleases: Creating placeholders for {Count} releases not yet downloaded", missingPublicationCodes.Count);
            using var scope2 = scopeFactory.CreateScope();
            var dbContext = scope2.ServiceProvider.GetRequiredService<MediaDbContext>();
            var publicationLanguageInfo = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Category)
                .Include(pl => pl.Language)
                .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode &&
                             pl.Category != null && pl.Category.CategoryCode == "Music" &&
                             missingPublicationCodes.Contains(pl.PublicationCode))
                .ToListAsync(cancellationToken);

            foreach (var plInfo in publicationLanguageInfo)
            {
                if (plInfo.Category == null || plInfo.Language == null)
                    continue;
                var placeholderPublication = new BiblePublication
                {
                    Id = 0,
                    PublicationCode = plInfo.PublicationCode,
                    Name = plInfo.PublicationCode,
                    BiblePublicationCategories = new List<BiblePublicationCategory> { new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = plInfo.CategoryId, Category = plInfo.Category } },
                    LanguageId = plInfo.LanguageId,
                    Language = plInfo.Language,
                    Sections = new List<BiblePublicationSection>(),
                    Tracks = new List<BiblePublicationTrack>(),
                    IsVideo = false,
                    IsMusic = true
                };
                var placeholder = new VocalMusic { Publication = placeholderPublication };
                result[placeholder.Code] = placeholder;
            }
        }

        Log.Information("GetVocalMusicReleases: Returning {TotalCount} vocal music releases ({DownloadedCount} downloaded, {PlaceholderCount} placeholders) for language={LanguageCode}",
            result.Count, downloadedReleases.Count, result.Count - downloadedReleases.Count, languageCode);

        return result;
    }
}
