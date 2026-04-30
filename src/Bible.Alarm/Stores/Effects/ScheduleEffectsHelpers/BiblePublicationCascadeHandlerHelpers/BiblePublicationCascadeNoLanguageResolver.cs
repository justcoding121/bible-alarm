#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;

/// <summary>
/// Resolves first section and track for a no-language publication (LanguageId == null).
/// </summary>
public static class BiblePublicationCascadeNoLanguageResolver
{
    public static async Task<(string? sectionCode, string? trackCode, string sectionName, string trackTitle, string publicationName)> GetFirstSectionAndTrackAsync(
        IMediaService mediaService,
        IServiceScopeFactory scopeFactory,
        string publicationCode)
    {
        string? sectionCode = null;
        string? trackCode = null;
        var sectionName = string.Empty;
        var trackTitle = string.Empty;
        var publicationName = string.Empty;

        using (var nameScope = scopeFactory.CreateScope())
        {
            var nameDb = nameScope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var pub = await nameDb.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.PublicationCode == publicationCode && bp.LanguageId == null)
                .FirstOrDefaultAsync();
            publicationName = pub?.Name ?? publicationCode;
        }

        var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

        if (sections != null && sections.Count > 0)
        {
            using var sectionEnumerator = sections.GetEnumerator();
            _ = sectionEnumerator.MoveNext();
            var firstSectionKvp = sectionEnumerator.Current;
            var firstSection = firstSectionKvp.Value;
            sectionCode = firstSection.SectionCode;
            sectionName = firstSection.Name;

            using var trackScope = scopeFactory.CreateScope();
            var trackDb = trackScope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var pub = await trackDb.BiblePublications
                .AsNoTracking()
                .Include(x => x.Sections)
                    .ThenInclude(s => s.Tracks)
                .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                .FirstOrDefaultAsync();

            if (pub?.Sections != null)
            {
                var section = pub.Sections.FirstOrDefault(s => SectionCodeHelper.CodeEquals(s.SectionCode, firstSection.SectionCode));
                if (section?.Tracks != null && section.Tracks.Count > 0)
                {
                    var firstTrack = section.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
                    trackCode = TrackCodeHelper.GetFromTrack(firstTrack);
                    trackTitle = firstTrack.Title ?? string.Empty;
                }
            }
        }
        else
        {
            using var trackScope = scopeFactory.CreateScope();
            var trackDb = trackScope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var pub = await trackDb.BiblePublications
                .AsNoTracking()
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                .FirstOrDefaultAsync();

            if (pub?.Tracks != null && pub.Tracks.Count > 0)
            {
                var firstTrack = pub.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
                trackCode = TrackCodeHelper.GetFromTrack(firstTrack);
                trackTitle = firstTrack.Title ?? string.Empty;
            }
        }

        return (sectionCode, trackCode, sectionName, trackTitle, publicationName);
    }
}
