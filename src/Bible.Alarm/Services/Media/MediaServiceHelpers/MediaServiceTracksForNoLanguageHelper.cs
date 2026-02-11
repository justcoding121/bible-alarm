#nullable enable
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Services.Media.MediaServiceHelpers;

/// <summary>
/// Loads tracks for no-language publications (LanguageId == null).
/// </summary>
public static class MediaServiceTracksForNoLanguageHelper
{
    public static async Task<SortedDictionary<string, BiblePublicationTrack>> GetTracksAsync(
        IServiceScopeFactory scopeFactory,
        string publicationCode,
        string? sectionCode,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var publicationId = await db.BiblePublications
            .AsNoTracking()
            .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (publicationId <= 0)
        {
            return new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer);
        }

        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            var flatTracks = await db.BiblePublicationTracks
                .AsNoTracking()
                .Where(t => t.BiblePublicationId == publicationId && t.BiblePublicationSectionId == null)
                .ToListAsync(cancellationToken);

            if (flatTracks.Count == 0)
            {
                return new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer);
            }

            var orderedTracks = flatTracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList();
            return new SortedDictionary<string, BiblePublicationTrack>(orderedTracks.ToDictionary(t => t.TrackCode, t => t), TrackCodeComparer.Comparer);
        }

        var sectionId = await db.BiblePublicationSections
            .AsNoTracking()
            .Where(s => s.BiblePublicationId == publicationId && s.SectionCode == sectionCode)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (sectionId <= 0)
        {
            return new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer);
        }

        var tracksList = await db.BiblePublicationTracks
            .AsNoTracking()
            .Where(t => t.BiblePublicationId == publicationId && t.BiblePublicationSectionId == sectionId)
            .ToListAsync(cancellationToken);

        if (tracksList.Count == 0)
        {
            return new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer);
        }

        var orderedTracksList = tracksList.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList();
        return new SortedDictionary<string, BiblePublicationTrack>(
            orderedTracksList.ToDictionary(t => t.TrackCode, t => t), TrackCodeComparer.Comparer);
    }
}
