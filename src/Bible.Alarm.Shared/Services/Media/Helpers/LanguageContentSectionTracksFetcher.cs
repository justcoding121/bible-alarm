#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class LanguageContentSectionTracksFetcher
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly SectionFetcher sectionFetcher;

    public LanguageContentSectionTracksFetcher(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        SectionFetcher sectionFetcher)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.sectionFetcher = sectionFetcher ?? throw new ArgumentNullException(nameof(sectionFetcher));
    }

    public async Task<bool> FetchSectionTracksAsync(
        string publicationCode,
        string sectionCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            // Get the publication for this language
            var publication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .Include(bp => bp.Sections)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (publication == null)
            {
                logger.Warning("Publication {PublicationCode} for language {LanguageCode} not found. Fetch sections first.",
                    publicationCode, languageCode);
                return false;
            }

            // Find the section - reload from database to ensure we have the correct IDs
            var section = await db.BiblePublicationSections
                .Include(s => s.UrlParams)
                .FirstOrDefaultAsync(
                    s => s.BiblePublicationId == publication.Id && s.SectionCode == normalizedSectionCode,
                    cancellationToken);

            if (section == null)
            {
                // Try to find by UrlParam as fallback - need to check sections that belong to this publication
                var allSections = await db.BiblePublicationSections
                    .Include(s => s.UrlParams)
                    .Where(s => s.BiblePublicationId == publication.Id)
                    .ToListAsync(cancellationToken);

                // Find section by SectionCode (UrlParams["booknum"] is only for URL construction)
                section = allSections.FirstOrDefault(s =>
                    s.SectionCode.Equals(normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
            }

            if (section == null)
            {
                logger.Warning("Section {SectionCode} not found in publication {PublicationCode} for language {LanguageCode}",
                    sectionCode, publicationCode, languageCode);
                return false;
            }

            // Check if language is available for this section
            // Use case-sensitive code for dramas when querying database
            var isAvailable = await db.SectionLanguages
                .Include(sl => sl.Language)
                .AnyAsync(
                    sl => sl.PublicationCode == publicationCodeForDb &&
                          sl.SectionCode == normalizedSectionCode &&
                          sl.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (!isAvailable)
            {
                logger.Warning("Language {LanguageCode} is not available for section {SectionCode} of publication {PublicationCode}",
                    languageCode, sectionCode, publicationCode);
                return false;
            }

            // Delete existing tracks for this section
            if (section.Tracks.Count > 0)
            {
                logger.Information("Deleting {Count} existing tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                    section.Tracks.Count, sectionCode, publicationCode, languageCode);
                db.BiblePublicationTracks.RemoveRange(section.Tracks);
                await db.SaveChangesAsync(cancellationToken);
                section.Tracks.Clear();
            }

            return await sectionFetcher.FetchSectionTracksAsync(
                db, normalizedPublicationCode, normalizedSectionCode, normalizedLanguageCode,
                publicationCodeForDb, publication, section, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching section tracks for {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                sectionCode, publicationCode, languageCode);
            return false;
        }
    }
}

