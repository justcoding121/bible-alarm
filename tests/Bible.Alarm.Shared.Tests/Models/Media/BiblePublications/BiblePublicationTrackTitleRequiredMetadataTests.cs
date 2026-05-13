#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackTitleRequiredMetadataTests
{
    [Fact]
    public void Title_is_marked_required()
    {
        var p = typeof(BiblePublicationTrack).GetProperty(nameof(BiblePublicationTrack.Title))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
