#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Services.Media.MediaServiceHelpers;

/// <summary>
/// Expected section/publication counts for discovery tables (SectionLanguages, PublicationLanguages).
/// </summary>
public static class MediaServiceExpectedCountHelper
{
    public static async Task<int> GetExpectedSectionCountAsync(
        IServiceScopeFactory scopeFactory,
        string? languageCode,
        string publicationCode,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

        var query = db.SectionLanguages
            .AsNoTracking()
            .Where(sl => sl.PublicationCode == publicationCode);

        if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            query = query.Where(sl =>
                (sl.Language != null && sl.Language.LanguageCode == normalizedLanguageCode) ||
                sl.LanguageId == null);
        }
        else
        {
            query = query.Where(sl => sl.LanguageId == null);
        }

        return await query
            .Select(sl => sl.SectionCode)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public static async Task<int> GetExpectedPublicationCountAsync(
        IServiceScopeFactory scopeFactory,
        string? languageCode,
        string categoryName,
        CancellationToken cancellationToken,
        bool requireIsMusicForMusicCategory = false)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

        var query = db.PublicationLanguages
            .AsNoTracking()
            .Where(pl => pl.Category != null && pl.Category.CategoryCode == categoryName);

        if (requireIsMusicForMusicCategory && string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(pl => pl.IsMusic);
        }

        if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            query = query.Where(pl =>
                (pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode) ||
                pl.LanguageId == null);
        }
        else
        {
            query = query.Where(pl => pl.LanguageId == null);
        }

        var publicationCodes = await query
            .Select(pl => pl.PublicationCode)
            .ToListAsync(cancellationToken);

        if (publicationCodes.Count == 0)
        {
            return 0;
        }

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in publicationCodes)
        {
            var lower = code.ToLowerInvariant();
            if (PublicationTypeHelper.IsDrama(lower))
            {
                unique.Add(lower.Equals("dramas", StringComparison.OrdinalIgnoreCase) ? AppConstants.Media.BiblePublicationCategoryDramas : "DramaticBibleReadings");
            }
            else
            {
                unique.Add(code);
            }
        }
        return unique.Count;
    }

    public static async Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(
        IServiceScopeFactory scopeFactory,
        string publicationCode,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        return await db.SectionLanguages
            .AsNoTracking()
            .Where(sl => sl.PublicationCode == publicationCode && sl.LanguageId == null)
            .Select(sl => sl.SectionCode)
            .Distinct()
            .CountAsync(cancellationToken);
    }
}
