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

    public async Task<SortedDictionary<string, BiblePublicationTrack>> GetTracksBySectionAsync(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

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
                return new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer);
            }

            var tracks = await dbContext.BiblePublicationTracks
                .AsNoTracking()
                .Where(t => t.BiblePublicationId == publicationId)
                .Where(t =>
                    string.IsNullOrWhiteSpace(sectionCode)
                        ? t.BiblePublicationSectionId == null
                        : t.Section != null && t.Section.SectionCode == sectionCode)
                .ToListAsync(cancellationToken);

            if (tracks.Count == 0)
            {
                return new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer);
            }

            // Order in memory using the custom comparer (EF Core can't translate custom comparers)
            var orderedTracks = tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList();
            // Build dictionary; allow duplicate TrackCodes (keep first) so we don't throw when data has duplicates
            var dict = new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer);
            foreach (var track in orderedTracks)
            {
                if (!dict.ContainsKey(track.TrackCode))
                {
                    dict[track.TrackCode] = track;
                }
                else
                {
                    logger.Debug("Duplicate TrackCode {TrackCode} for publication {PublicationCode} language {LanguageCode}, skipping",
                        track.TrackCode, publicationCode, languageCode);
                }
            }
            return dict;
        }
        catch (Exception ex)
        {
            logger.Error(ex,
                "Error getting BiblePublicationTracks by section. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionCode={SectionCode}",
                languageCode, publicationCode, sectionCode);
            throw;
        }
    }

    public async Task<BiblePublicationTrack?> GetTrackAsync(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        string trackCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

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

            var query = dbContext.BiblePublicationTracks
                .AsNoTracking()
                .Include(t => t.Section)
                .Include(t => t.TrackUrl)
                .Where(t => t.BiblePublicationId == publicationId)
                .Where(t =>
                    string.IsNullOrWhiteSpace(sectionCode)
                        ? t.BiblePublicationSectionId == null
                        : t.Section != null && t.Section.SectionCode == sectionCode);

            // Query by TrackCode (string)
            query = query.Where(t => t.TrackCode == trackCode);

            return await query.SingleOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex,
                "Error getting BiblePublicationTrack. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}",
                languageCode, publicationCode, sectionCode, trackCode);
            throw;
        }
    }

    public async Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string? sectionCode, string trackCode, string url, CancellationToken cancellationToken = default)
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

