#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionsRequiredMetadataTests
{
    [Fact]
    public void Sections_collection_is_marked_required()
    {
        var p = typeof(BiblePublication).GetProperty(nameof(BiblePublication.Sections))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
