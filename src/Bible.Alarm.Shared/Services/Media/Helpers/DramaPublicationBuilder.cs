#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for building and saving drama publications.
/// One publication row per (PublicationCode, LanguageId); categories are for UX filtering only.
/// </summary>
internal sealed class DramaPublicationBuilder
{
    private readonly ILogger logger;

    public DramaPublicationBuilder(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Upserts a drama publication: if a BiblePublication with the same PublicationCode and LanguageId exists,
    /// updates it (replaces tracks and UrlParams, syncs categories); otherwise inserts a new row.
    /// Callers: (1) Harvester only calls this via SeedEnglishPublicationAsync for pubs that do not yet have English, so insert is the normal path.
    /// (2) App on-demand fetch uses EnsurePublicationExistsAsync which skips when the row exists, or FetchPublicationTracksAsync which deletes then re-fetches; neither hits this with an existing row.
    /// The update path is for idempotency: e.g. SeedEnglishPublicationAsync invoked twice (race, future refresh-English, or logic change), so we update instead of creating a duplicate row.
    /// </summary>
    public async Task<bool> BuildAndSavePublicationAsync(
        MediaDbContext db,
        string publicationCodeForDb,
        string? publicationName,
        Language language,
        Category category,
        List<BiblePublicationTrack> tracks,
        CancellationToken cancellationToken)
    {
        if (tracks.Count == 0)
        {
            logger.Warning("No tracks found for drama {PublicationCode}", publicationCodeForDb);
            return false;
        }

        var finalPublicationName = publicationName ?? publicationCodeForDb;
        var isVideo = PublicationTypeHelper.IsVideo(publicationCodeForDb);
        var categoryCodes = JwSourceHelper.GetCategoryCodesForPublication(publicationCodeForDb);
        var categories = await db.Categories
            .Where(c => categoryCodes.Contains(c.CategoryCode))
            .ToListAsync(cancellationToken);

        var existingPublication = await db.BiblePublications
            .Include(bp => bp.Tracks)
            .ThenInclude(t => t.UrlParams)
            .Include(bp => bp.BiblePublicationCategories)
            .ThenInclude(bpc => bpc.Category)
            .FirstOrDefaultAsync(
                bp => bp.PublicationCode == publicationCodeForDb && bp.LanguageId == language.Id,
                cancellationToken);

        if (existingPublication != null)
        {
            foreach (var track in existingPublication.Tracks)
            {
                if (track.UrlParams.Count > 0)
                {
                    db.UrlParams.RemoveRange(track.UrlParams);
                }
            }

            db.BiblePublicationTracks.RemoveRange(existingPublication.Tracks);
            existingPublication.Tracks.Clear();
            existingPublication.Name = finalPublicationName;
            existingPublication.IsVideo = isVideo;
            existingPublication.IsMusic = categories.Any(c => c.CategoryCode.Equals("Music", StringComparison.OrdinalIgnoreCase));

            SyncPublicationCategories(existingPublication, categories);

            foreach (var track in tracks)
            {
                track.Publication = existingPublication;
                track.Section = null;
                track.BiblePublicationId = existingPublication.Id;
                existingPublication.Tracks.Add(track);
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.Information("Updated existing publication {PublicationCode} with {Count} tracks",
                publicationCodeForDb, tracks.Count);
            return true;
        }

        var isMusicPub = categories.Any(c => c.CategoryCode.Equals("Music", StringComparison.OrdinalIgnoreCase));
        var publication = new BiblePublication
        {
            PublicationCode = publicationCodeForDb,
            Name = finalPublicationName,
            Language = language,
            BiblePublicationCategories = categories
                .Select(cat => new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = cat.Id, Category = cat })
                .ToList(),
            LanguageId = language.Id,
            IsVideo = isVideo,
            IsMusic = isMusicPub,
            Tracks = tracks,
            Sections = new List<BiblePublicationSection>()
        };

        foreach (var track in tracks)
        {
            track.Publication = publication;
            track.Section = null;
        }

        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        logger.Information("Successfully fetched {Count} tracks for drama {PublicationCode}",
            tracks.Count, publicationCodeForDb);

        return true;
    }

    private static void SyncPublicationCategories(BiblePublication publication, List<Category> categories)
    {
        var existingCategoryIds = publication.BiblePublicationCategories
            .Select(bpc => bpc.CategoryId)
            .ToHashSet();
        foreach (var cat in categories)
        {
            if (existingCategoryIds.Add(cat.Id))
            {
                publication.BiblePublicationCategories.Add(
                    new BiblePublicationCategory { BiblePublicationId = publication.Id, CategoryId = cat.Id, Category = cat });
            }
        }
    }

    public string GetPublicationCodeForDb(string normalizedPublicationCode)
    {
        var canonical = Bible.Alarm.Shared.Helpers.JwSourceHelper.GetCanonicalDramaPublicationCode(normalizedPublicationCode);
        if (canonical != null)
        {
            return canonical;
        }

        logger.Warning("Unknown drama publication code: {PublicationCode}", normalizedPublicationCode);
        return normalizedPublicationCode;
    }
}

