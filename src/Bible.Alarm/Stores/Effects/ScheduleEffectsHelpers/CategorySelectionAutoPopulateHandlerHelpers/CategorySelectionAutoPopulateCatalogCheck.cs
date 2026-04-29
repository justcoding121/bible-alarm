#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.CategorySelectionAutoPopulateHandlerHelpers;

/// <summary>
/// Checks if a publication with its first section and tracks is already cataloged.
/// </summary>
public static class CategorySelectionAutoPopulateCatalogCheck
{
    public static async Task<bool> CheckIfPublicationWithFirstSectionCatalogedAsync(
        ILogger logger,
        MediaDbContext db,
        string publicationCode,
        string normalizedLanguageCode)
    {
        try
        {
            var lowerCode = publicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(lowerCode);
            var publicationCodeForDb = isDrama
                ? (lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase) ? AppConstants.Media.BiblePublicationCategoryDramas : "DramaticBibleReadings")
                : publicationCode;

            var publicationId = await db.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.PublicationCode == publicationCodeForDb &&
                             bp.Language != null &&
                             bp.Language.LanguageCode == normalizedLanguageCode)
                .Select(bp => bp.Id)
                .FirstOrDefaultAsync();

            if (publicationId <= 0)
                return false;

            var sectionCodes = await db.SectionLanguages
                .AsNoTracking()
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                             sl.Language != null &&
                             sl.Language.LanguageCode == normalizedLanguageCode)
                .Select(sl => sl.SectionCode)
                .ToListAsync();

            var firstSectionCode = sectionCodes
                .OrderBy(sc => sc, SectionCodeHelper.SectionCodeComparer)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(firstSectionCode))
            {
                var hasTracks = await db.BiblePublicationTracks
                    .AsNoTracking()
                    .AnyAsync(t => t.BiblePublicationId == publicationId && t.BiblePublicationSectionId == null);
                return hasTracks;
            }

            var sections = await db.BiblePublicationSections
                .AsNoTracking()
                .Where(s => s.BiblePublicationId == publicationId)
                .Select(s => new { s.Id, s.SectionCode })
                .ToListAsync();

            var firstSection = sections.FirstOrDefault(s =>
                string.Equals(s.SectionCode, firstSectionCode, StringComparison.OrdinalIgnoreCase));
            var firstSectionId = firstSection?.Id ?? 0;

            if (firstSectionId <= 0)
                return false;

            return await db.BiblePublicationTracks
                .AsNoTracking()
                .AnyAsync(t => t.BiblePublicationId == publicationId && t.BiblePublicationSectionId == firstSectionId);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "CategorySelectionAutoPopulateHandler: Error checking if publication {PublicationCode} is cataloged", publicationCode);
            return false;
        }
    }
}
