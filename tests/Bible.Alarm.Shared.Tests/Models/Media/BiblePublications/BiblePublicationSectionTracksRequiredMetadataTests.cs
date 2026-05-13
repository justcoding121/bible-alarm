#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionTracksRequiredMetadataTests
{
    [Fact]
    public void Tracks_collection_is_marked_required()
    {
        var p = typeof(BiblePublicationSection).GetProperty(nameof(BiblePublicationSection.Tracks))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
