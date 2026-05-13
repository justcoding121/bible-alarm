#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationIsVideoRequiredMetadataTests
{
    [Fact]
    public void IsVideo_is_marked_required()
    {
        var p = typeof(BiblePublication).GetProperty(nameof(BiblePublication.IsVideo))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
