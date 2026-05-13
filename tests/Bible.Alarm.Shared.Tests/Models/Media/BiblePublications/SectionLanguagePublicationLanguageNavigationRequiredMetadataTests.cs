#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguagePublicationLanguageNavigationRequiredMetadataTests
{
    [Fact]
    public void PublicationLanguage_navigation_is_marked_required()
    {
        var p = typeof(SectionLanguage).GetProperty(nameof(SectionLanguage.PublicationLanguage))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
