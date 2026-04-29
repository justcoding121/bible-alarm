#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
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

            var sectionFetcher = new SectionFetcher(httpClient, logger);
            return await sectionFetcher.FetchSectionTracksAsync(new FetchSectionTracksRequest(
                db,
                normPub.ToLowerInvariant(),
                normSection,
                AppConstants.Media.DefaultLanguageCode,
                normPub,
                publication,
                section,
                cancellationToken,
                ReplaceExisting: true));
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
