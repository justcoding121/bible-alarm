#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
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
        var normalizedLanguageCode = NormalizeLanguage(languageCode);

        var query = db.SectionLanguages
            .AsNoTracking()
            .Where(sl => sl.PublicationCode == publicationCode);

        query = ApplySectionLanguageRestriction(query, normalizedLanguageCode);

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
        var normalizedLanguageCode = NormalizeLanguage(languageCode);

        var query = PublicationLanguagesForCategoryQuery(db, categoryName, requireIsMusicForMusicCategory);
        query = ApplyPublicationLanguageRestriction(query, normalizedLanguageCode);

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
                unique.Add(lower.Equals("dramas", StringComparison.OrdinalIgnoreCase) ? AppConstants.Media.BiblePublicationCategoryDramas : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings);
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

    private static string? NormalizeLanguage(string? languageCode) =>
        string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

    private static IQueryable<SectionLanguage> ApplySectionLanguageRestriction(IQueryable<SectionLanguage> query, string? normalizedLanguageCode)
    {
        if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            return query.Where(sl =>
                (sl.Language != null && sl.Language.LanguageCode == normalizedLanguageCode) ||
                sl.LanguageId == null);
        }

        return query.Where(sl => sl.LanguageId == null);
    }

    private static IQueryable<PublicationLanguage> PublicationLanguagesForCategoryQuery(
        MediaDbContext db,
        string categoryName,
        bool requireIsMusicForMusicCategory)
    {
        var query = db.PublicationLanguages
            .AsNoTracking()
            .Where(pl => pl.Category != null && pl.Category.CategoryCode == categoryName);

        if (!requireIsMusicForMusicCategory || !string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        return query.Where(pl => pl.IsMusic);
    }

    private static IQueryable<PublicationLanguage> ApplyPublicationLanguageRestriction(
        IQueryable<PublicationLanguage> query,
        string? normalizedLanguageCode)
    {
        if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            return query.Where(pl =>
                (pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode) ||
                pl.LanguageId == null);
        }

        return query.Where(pl => pl.LanguageId == null);
    }
}
