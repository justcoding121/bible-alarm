#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackAnnotationsTests
{
    [Fact]
    public void Has_non_unique_publication_track_index_and_unique_section_track_index()
    {
        var indexes = typeof(BiblePublicationTrack).GetCustomAttributes(typeof(IndexAttribute), inherit: false)
            .Cast<IndexAttribute>()
            .ToList();

        _ = Assert.Single(indexes, a =>
            !a.IsUnique &&
            a.PropertyNames.SequenceEqual(new[]
            {
                nameof(BiblePublicationTrack.BiblePublicationId),
                nameof(BiblePublicationTrack.TrackCode),
            }));

        _ = Assert.Single(indexes, a =>
            a.IsUnique &&
            a.PropertyNames.SequenceEqual(new[]
            {
                nameof(BiblePublicationTrack.BiblePublicationSectionId),
                nameof(BiblePublicationTrack.TrackCode),
            }));
    }
}
