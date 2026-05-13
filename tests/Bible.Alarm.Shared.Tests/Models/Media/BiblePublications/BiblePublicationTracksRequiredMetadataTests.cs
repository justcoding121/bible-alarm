#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTracksRequiredMetadataTests
{
    [Fact]
    public void Tracks_collection_is_marked_required()
    {
        var p = typeof(BiblePublication).GetProperty(nameof(BiblePublication.Tracks))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
