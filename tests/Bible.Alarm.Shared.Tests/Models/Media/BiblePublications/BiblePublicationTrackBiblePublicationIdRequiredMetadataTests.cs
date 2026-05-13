#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackBiblePublicationIdRequiredMetadataTests
{
    [Fact]
    public void BiblePublicationId_is_marked_required()
    {
        var p = typeof(BiblePublicationTrack).GetProperty(nameof(BiblePublicationTrack.BiblePublicationId))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
