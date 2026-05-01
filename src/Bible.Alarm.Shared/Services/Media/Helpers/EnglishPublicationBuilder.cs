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
            return await UpdateExistingPublicationAsync(new EnglishPublicationUpdateRequest(
                db,
                existingPublication,
                categories,
                sections,
                finalPublicationName,
                isVideo,
                isBible,
                publicationWithoutLanguage,
                normalizedPublicationCode,
                cancellationToken));
        }

        return await InsertNewPublicationAsync(new EnglishPublicationInsertRequest(
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
            cancellationToken));
    }

    private static bool PublicationIndicatesMusic(List<Category> categories, string publicationCode) =>
        categories.Any(c => c.CategoryCode.Equals(AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase)) ||
        JwSourceHelper.MusicFlagPublicationCodes.Contains(publicationCode);

    private async Task<bool> UpdateExistingPublicationAsync(EnglishPublicationUpdateRequest req)
    {
        ClearTracksAndSections(req.Db, req.ExistingPublication);

        req.ExistingPublication.Name = req.FinalPublicationName;
        req.ExistingPublication.IsVideo = req.IsVideo;
        req.ExistingPublication.IsMusic = PublicationIndicatesMusic(req.Categories, req.ExistingPublication.PublicationCode);
        SyncPublicationCategories(req.ExistingPublication, req.Categories);
        AttachSectionsToPublication(req.ExistingPublication, req.Sections);

        await req.Db.SaveChangesAsync(req.CancellationToken);

        if (req.IsBible && !req.PublicationWithoutLanguage)
        {
            AssignBibleTrackForeignKeys(req.Sections, req.ExistingPublication.Id);
            await req.Db.SaveChangesAsync(req.CancellationToken);
        }

        logger.Information("Updated existing publication {PublicationCode} with {Count} sections",
            req.NormalizedPublicationCode, req.Sections.Count);
        return true;
    }

    private static void ClearTracksAndSections(MediaDbContext db, BiblePublication existingPublication)
    {
        foreach (var track in existingPublication.Sections.SelectMany(s => s.Tracks).Where(t => t.TrackUrl != null))
        {
            db.TrackUrls.Remove(track.TrackUrl!);
        }

        db.BiblePublicationTracks.RemoveRange(existingPublication.Sections.SelectMany(s => s.Tracks));
        db.BiblePublicationSections.RemoveRange(existingPublication.Sections);
        existingPublication.Sections.Clear();
    }

    private static void AttachSectionsToPublication(BiblePublication publication, List<BiblePublicationSection> sections)
    {
        foreach (var section in sections)
        {
            section.BiblePublication = publication;
            section.BiblePublicationId = publication.Id;
            foreach (var track in section.Tracks)
            {
                track.Publication = publication;
                track.Section = section;
            }
            publication.Sections.Add(section);
        }
    }

    private static void AssignBibleTrackForeignKeys(List<BiblePublicationSection> sections, int publicationId)
    {
        foreach (var section in sections)
        {
            foreach (var track in section.Tracks)
            {
                track.BiblePublicationId = publicationId;
                track.BiblePublicationSectionId = section.Id;
            }
        }
    }

    private async Task<bool> InsertNewPublicationAsync(EnglishPublicationInsertRequest req)
    {
        var tracksBySection = StageBibleTracksIfNeeded(req.Sections, req.IsBible, req.PublicationWithoutLanguage);

        var isMusicPub = PublicationIndicatesMusic(req.Categories, req.NormalizedPublicationCode);
        var publication = BuildNewPublicationEntity(req, isMusicPub);

        WireSectionsForInsert(publication, req.Sections, req.IsBible, req.PublicationWithoutLanguage);

        req.Db.BiblePublications.Add(publication);
        await req.Db.SaveChangesAsync(req.CancellationToken);

        await RestoreStagedBibleTracksAsync(
            req.Db, publication, tracksBySection, req.IsBible, req.PublicationWithoutLanguage, req.CancellationToken);

        logger.Information("Successfully seeded {Count} sections for English publication {PublicationCode}",
            req.Sections.Count, req.NormalizedPublicationCode);

        return true;
    }

    private static Dictionary<BiblePublicationSection, List<BiblePublicationTrack>> StageBibleTracksIfNeeded(
        List<BiblePublicationSection> sections,
        bool isBible,
        bool publicationWithoutLanguage)
    {
        var tracksBySection = new Dictionary<BiblePublicationSection, List<BiblePublicationTrack>>();
        if (!isBible || publicationWithoutLanguage)
        {
            return tracksBySection;
        }

        foreach (var section in sections)
        {
            if (section.Tracks.Count > 0)
            {
                tracksBySection[section] = section.Tracks.ToList();
                section.Tracks.Clear();
            }
        }

        return tracksBySection;
    }

    private static BiblePublication BuildNewPublicationEntity(EnglishPublicationInsertRequest req, bool isMusicPub) =>
        new()
        {
            PublicationCode = req.NormalizedPublicationCode,
            Name = req.FinalPublicationName,
            Language = req.Language,
            BiblePublicationCategories = req.Categories
                .Select(cat => new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = cat.Id, Category = cat })
                .ToList(),
            LanguageId = req.LanguageId,
            IsVideo = req.IsVideo,
            IsMusic = isMusicPub,
            CatalogType = CatalogType.Sectioned,
            Tracks = new List<BiblePublicationTrack>(),
            Sections = req.Sections
        };

    private static void WireSectionsForInsert(
        BiblePublication publication,
        List<BiblePublicationSection> sections,
        bool isBible,
        bool publicationWithoutLanguage)
    {
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
    }

    private static async Task RestoreStagedBibleTracksAsync(
        MediaDbContext db,
        BiblePublication publication,
        Dictionary<BiblePublicationSection, List<BiblePublicationTrack>> tracksBySection,
        bool isBible,
        bool publicationWithoutLanguage,
        CancellationToken cancellationToken)
    {
        if (!isBible || publicationWithoutLanguage || tracksBySection.Count == 0)
        {
            return;
        }

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
