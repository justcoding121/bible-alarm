#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackTrackCodeRequiredMetadataTests
{
    [Fact]
    public void TrackCode_is_marked_required()
    {
        var p = typeof(BiblePublicationTrack).GetProperty(nameof(BiblePublicationTrack.TrackCode))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
