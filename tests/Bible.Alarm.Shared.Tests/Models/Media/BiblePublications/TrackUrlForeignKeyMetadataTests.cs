#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackUrlForeignKeyMetadataTests
{
    [Fact]
    public void Bible_publication_track_foreign_key_targets_track_navigation()
    {
        var fk = typeof(TrackUrl).GetProperty(nameof(TrackUrl.BiblePublicationTrackId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(TrackUrl.BiblePublicationTrack), fk.Name);
    }
}
