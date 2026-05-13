#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionBiblePublicationNavigationRequiredMetadataTests
{
    [Fact]
    public void BiblePublication_navigation_is_marked_required()
    {
        var p = typeof(BiblePublicationSection).GetProperty(nameof(BiblePublicationSection.BiblePublication))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
