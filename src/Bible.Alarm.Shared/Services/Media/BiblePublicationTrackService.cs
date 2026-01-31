#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing BiblePublicationTrack database operations.
/// </summary>
public sealed class BiblePublicationTrackService(IServiceScopeFactory scopeFactory, ILogger logger) : IBiblePublicationTrackService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;

    public async Task<SortedDictionary<int, BiblePublicationTrack>> GetTracksBySectionAsync(string languageCode, string publicationCode, int sectionCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();
            var sectionCodeString = sectionCode.ToString();

            // Fast path: query only the section's tracks (avoid loading all sections and tracks).
            var publicationId = await dbContext.BiblePublications
                .AsNoTracking()
                .Where(p => p.PublicationCode == publicationCode &&
                            p.Language != null &&
                            p.Language.LanguageCode == normalizedLanguageCode)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (publicationId <= 0)
            {
                return new SortedDictionary<int, BiblePublicationTrack>();
            }

            var sectionId = await dbContext.BiblePublicationSections
                .AsNoTracking()
                .Where(s => s.BiblePublicationId == publicationId && s.SectionCode == sectionCodeString)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (sectionId <= 0)
            {
                return new SortedDictionary<int, BiblePublicationTrack>();
            }

            var tracks = await dbContext.BiblePublicationTracks
                .AsNoTracking()
                .Where(t => t.BiblePublicationId == publicationId && t.BiblePublicationSectionId == sectionId)
                .OrderBy(t => t.Number)
                .ToListAsync(cancellationToken);

            return tracks.Count == 0
                ? new SortedDictionary<int, BiblePublicationTrack>()
                : new SortedDictionary<int, BiblePublicationTrack>(tracks.ToDictionary(x => x.Number, x => x));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationTracks by section. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionCode={SectionCode}",
                languageCode, publicationCode, sectionCode);
            throw;
        }
    }

    public async Task<BiblePublicationTrack?> GetTrackAsync(string languageCode, string publicationCode, int sectionCode, int trackNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();
            var sectionCodeString = sectionCode.ToString();

            var publicationId = await dbContext.BiblePublications
                .AsNoTracking()
                .Where(p => p.PublicationCode == publicationCode &&
                            p.Language != null &&
                            p.Language.LanguageCode == normalizedLanguageCode)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (publicationId <= 0)
            {
                return null;
            }

            var sectionId = await dbContext.BiblePublicationSections
                .AsNoTracking()
                .Where(s => s.BiblePublicationId == publicationId && s.SectionCode == sectionCodeString)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (sectionId <= 0)
            {
                return null;
            }

            return await dbContext.BiblePublicationTracks
                .AsNoTracking()
                .Where(t => t.BiblePublicationId == publicationId &&
                            t.BiblePublicationSectionId == sectionId &&
                            t.Number == trackNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationTrack. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackNumber={TrackNumber}",
                languageCode, publicationCode, sectionCode, trackNumber);
            throw;
        }
    }

    public async Task UpdateTrackUrlAsync(string languageCode, string publicationCode, int sectionCode, int trackNumber, string url, CancellationToken cancellationToken = default)
    {
        // URLs are now computed on-demand, no need to store them
        // This method is kept for backward compatibility but does nothing
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
    }
}

