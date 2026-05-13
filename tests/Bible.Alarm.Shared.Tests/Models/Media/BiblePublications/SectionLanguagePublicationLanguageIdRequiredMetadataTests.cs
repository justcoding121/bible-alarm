#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguagePublicationLanguageIdRequiredMetadataTests
{
    [Fact]
    public void PublicationLanguageId_is_marked_required()
    {
        var p = typeof(SectionLanguage).GetProperty(nameof(SectionLanguage.PublicationLanguageId))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
