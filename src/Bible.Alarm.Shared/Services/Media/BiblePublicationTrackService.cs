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

    public async Task<SortedDictionary<int, BiblePublicationTrack>> GetTracksBySectionAsync(string languageCode, string publicationCode, int sectionNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(p => p.Sections.Where(s => s.Number == sectionNumber))
                    .ThenInclude(s => s.Tracks)
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (publication == null)
            {
                return new SortedDictionary<int, BiblePublicationTrack>();
            }

            var section = publication.Sections.FirstOrDefault(s => s.Number == sectionNumber);
            if (section == null)
            {
                return new SortedDictionary<int, BiblePublicationTrack>();
            }

            var tracks = section.Tracks.OrderBy(t => t.Number).ToList();
            return new SortedDictionary<int, BiblePublicationTrack>(tracks.ToDictionary(x => x.Number, x => x));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationTracks by section. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}",
                languageCode, publicationCode, sectionNumber);
            throw;
        }
    }

    public async Task<BiblePublicationTrack?> GetTrackAsync(string languageCode, string publicationCode, int sectionNumber, int trackNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(p => p.Sections.Where(s => s.Number == sectionNumber))
                    .ThenInclude(s => s.Tracks.Where(t => t.Number == trackNumber))
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            return publication?.Sections
                .FirstOrDefault(s => s.Number == sectionNumber)?
                .Tracks.FirstOrDefault(t => t.Number == trackNumber);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationTrack. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}, TrackNumber={TrackNumber}",
                languageCode, publicationCode, sectionNumber, trackNumber);
            throw;
        }
    }

    public async Task UpdateTrackUrlAsync(string languageCode, string publicationCode, int sectionNumber, int trackNumber, string url, CancellationToken cancellationToken = default)
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

