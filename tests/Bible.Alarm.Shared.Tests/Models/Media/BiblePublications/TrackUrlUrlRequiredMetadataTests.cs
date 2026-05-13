#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackUrlUrlRequiredMetadataTests
{
    [Fact]
    public void Url_is_marked_required()
    {
        var p = typeof(TrackUrl).GetProperty(nameof(TrackUrl.Url))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
