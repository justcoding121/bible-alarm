#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackPublicationNavigationRequiredMetadataTests
{
    [Fact]
    public void Publication_navigation_is_marked_required()
    {
        var p = typeof(BiblePublicationTrack).GetProperty(nameof(BiblePublicationTrack.Publication))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
