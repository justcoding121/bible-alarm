#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <inheritdoc />
public sealed class MelodyDiscTracksApiRefresher(
    IServiceScopeFactory scopeFactory,
    System.Net.Http.HttpClient httpClient,
    ILogger logger) : IMelodyDiscTracksApiRefresher
{
    public async Task<bool> ReplaceDiscSectionTracksFromApiAsync(
        string publicationCode,
        string discSectionCode,
        CancellationToken cancellationToken = default)
    {
        var normPub = publicationCode.Trim();
        var normSection = discSectionCode.Trim().ToLowerInvariant();

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publication = await db.BiblePublications
                .Include(p => p.BiblePublicationCategories)
                .ThenInclude(b => b.Category)
                .FirstOrDefaultAsync(
                    p => p.PublicationCode == normPub && p.LanguageId == null,
                    cancellationToken);

            if (publication == null)
            {
                logger.Warning(
                    "MelodyDiscTracksApiRefresher: no-language publication {PublicationCode} not found",
                    publicationCode);
                return false;
            }

            var section = await db.BiblePublicationSections
                .FirstOrDefaultAsync(
                    s => s.BiblePublicationId == publication.Id && s.SectionCode == normSection,
                    cancellationToken);

            if (section == null)
            {
                logger.Warning(
                    "MelodyDiscTracksApiRefresher: section {SectionCode} not found for publication {PublicationCode}",
                    discSectionCode,
                    publicationCode);
                return false;
            }

            var existingTracks = await db.BiblePublicationTracks
                .Where(t => t.BiblePublicationSectionId == section.Id)
                .ToListAsync(cancellationToken);

            if (existingTracks.Count > 0)
            {
                db.BiblePublicationTracks.RemoveRange(existingTracks);
                await db.SaveChangesAsync(cancellationToken);
            }

            var sectionFetcher = new SectionFetcher(httpClient, logger);
            return await sectionFetcher.FetchSectionTracksAsync(
                db,
                normPub.ToLowerInvariant(),
                normSection,
                "E",
                normPub,
                publication,
                section,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(
                ex,
                "MelodyDiscTracksApiRefresher failed for publication {PublicationCode} section {SectionCode}",
                publicationCode,
                discSectionCode);
            return false;
        }
    }
}
