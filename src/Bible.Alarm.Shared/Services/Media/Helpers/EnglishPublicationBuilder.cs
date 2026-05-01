#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for building and saving English publications (Bible, etc.).
/// One publication row per (PublicationCode, LanguageId); categories are for UX filtering only.
/// </summary>
internal sealed class EnglishPublicationBuilder
{
    private readonly ILogger logger;

    public EnglishPublicationBuilder(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> BuildAndSavePublicationAsync(BuildEnglishPublicationRequest request)
    {
        var db = request.Db;
        var normalizedPublicationCode = request.NormalizedPublicationCode;
        var publicationName = request.PublicationName;
        var language = request.Language;
        var isVideo = request.IsVideo;
        var isBible = request.IsBible;
        var publicationWithoutLanguage = request.PublicationWithoutLanguage;
        var sections = request.Sections;
        var cancellationToken = request.CancellationToken;

        if (sections.Count == 0)
        {
            logger.Warning("No sections found for publication {PublicationCode} in English", normalizedPublicationCode);
            return false;
        }

        var finalPublicationName = publicationName ?? normalizedPublicationCode;
        var categoryCodes = JwSourceHelper.GetCategoryCodesForPublication(normalizedPublicationCode);
        var categories = await db.Categories
            .Where(c => categoryCodes.Contains(c.CategoryCode))
            .ToListAsync(cancellationToken);

        var languageId = language?.Id;
        var existingPublication = await db.BiblePublications
            .Include(bp => bp.Sections)
            .ThenInclude(s => s.Tracks)
            .ThenInclude(t => t.TrackUrl)
            .Include(bp => bp.BiblePublicationCategories)
            .ThenInclude(bpc => bpc.Category)
            .FirstOrDefaultAsync(
                bp => bp.PublicationCode == normalizedPublicationCode && bp.LanguageId == languageId,
                cancellationToken);

        if (existingPublication != null)
        {
            return await UpdateExistingPublicationAsync(
                db,
                existingPublication,
                categories,
                sections,
                finalPublicationName,
                isVideo,
                isBible,
                publicationWithoutLanguage,
                normalizedPublicationCode,
                cancellationToken);
        }

        return await InsertNewPublicationAsync(
            db,
            categories,
            sections,
            normalizedPublicationCode,
            finalPublicationName,
            language,
            languageId,
            isVideo,
            isBible,
            publicationWithoutLanguage,
            cancellationToken);
    }

    private static bool PublicationIndicatesMusic(List<Category> categories, string publicationCode) =>
        categories.Any(c => c.CategoryCode.Equals(AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase)) ||
        JwSourceHelper.MusicFlagPublicationCodes.Contains(publicationCode);

    private async Task<bool> UpdateExistingPublicationAsync(
        MediaDbContext db,
        BiblePublication existingPublication,
        List<Category> categories,
        List<BiblePublicationSection> sections,
        string finalPublicationName,
        bool isVideo,
        bool isBible,
        bool publicationWithoutLanguage,
        string normalizedPublicationCode,
        CancellationToken cancellationToken)
    {
        foreach (var track in existingPublication.Sections.SelectMany(s => s.Tracks).Where(t => t.TrackUrl != null))
        {
            db.TrackUrls.Remove(track.TrackUrl!);
        }

        db.BiblePublicationTracks.RemoveRange(existingPublication.Sections.SelectMany(s => s.Tracks));
        db.BiblePublicationSections.RemoveRange(existingPublication.Sections);
        existingPublication.Sections.Clear();
        existingPublication.Name = finalPublicationName;
        existingPublication.IsVideo = isVideo;
        existingPublication.IsMusic = PublicationIndicatesMusic(categories, existingPublication.PublicationCode);
        SyncPublicationCategories(existingPublication, categories);

        foreach (var section in sections)
        {
            section.BiblePublication = existingPublication;
            section.BiblePublicationId = existingPublication.Id;
            foreach (var track in section.Tracks)
            {
                track.Publication = existingPublication;
                track.Section = section;
            }
            existingPublication.Sections.Add(section);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (isBible && !publicationWithoutLanguage)
        {
            foreach (var section in sections)
            {
                foreach (var track in section.Tracks)
                {
                    track.BiblePublicationId = existingPublication.Id;
                    track.BiblePublicationSectionId = section.Id;
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.Information("Updated existing publication {PublicationCode} with {Count} sections",
            normalizedPublicationCode, sections.Count);
        return true;
    }

    private async Task<bool> InsertNewPublicationAsync(
        MediaDbContext db,
        List<Category> categories,
        List<BiblePublicationSection> sections,
        string normalizedPublicationCode,
        string finalPublicationName,
        Language? language,
        int? languageId,
        bool isVideo,
        bool isBible,
        bool publicationWithoutLanguage,
        CancellationToken cancellationToken)
    {
        var tracksBySection = new Dictionary<BiblePublicationSection, List<BiblePublicationTrack>>();
        if (isBible && !publicationWithoutLanguage)
        {
            foreach (var section in sections)
            {
                if (section.Tracks.Count > 0)
                {
                    tracksBySection[section] = section.Tracks.ToList();
                    section.Tracks.Clear();
                }
            }
        }

        var isMusicPub = PublicationIndicatesMusic(categories, normalizedPublicationCode);
        var publication = new BiblePublication
        {
            PublicationCode = normalizedPublicationCode,
            Name = finalPublicationName,
            Language = language,
            BiblePublicationCategories = categories
                .Select(cat => new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = cat.Id, Category = cat })
                .ToList(),
            LanguageId = languageId,
            IsVideo = isVideo,
            IsMusic = isMusicPub,
            CatalogType = CatalogType.Sectioned,
            Tracks = new List<BiblePublicationTrack>(),
            Sections = sections
        };

        foreach (var section in sections)
        {
            section.BiblePublication = publication;
            if (!isBible || publicationWithoutLanguage)
            {
                foreach (var track in section.Tracks)
                {
                    track.Publication = publication;
                    track.Section = section;
                }
            }
        }

        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        if (isBible && !publicationWithoutLanguage && tracksBySection.Count > 0)
        {
            foreach (var kvp in tracksBySection)
            {
                var section = kvp.Key;
                var tracks = kvp.Value;
                foreach (var track in tracks)
                {
                    track.Publication = publication;
                    track.Section = section;
                    track.BiblePublicationId = publication.Id;
                    track.BiblePublicationSectionId = section.Id;
                }
                foreach (var track in tracks)
                {
                    section.Tracks.Add(track);
                }
                db.BiblePublicationTracks.AddRange(tracks);
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.Information("Successfully seeded {Count} sections for English publication {PublicationCode}",
            sections.Count, normalizedPublicationCode);

        return true;
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
