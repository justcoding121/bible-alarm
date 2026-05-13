#nullable enable

using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class TrackNavigationResultRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var track = new BiblePublicationTrack();
        var a = new TrackNavigationResult("pub", null, track);
        var b = new TrackNavigationResult(a.PublicationCode, a.Section, a.Track);
        Assert.Equal(a, b);
    }
}
