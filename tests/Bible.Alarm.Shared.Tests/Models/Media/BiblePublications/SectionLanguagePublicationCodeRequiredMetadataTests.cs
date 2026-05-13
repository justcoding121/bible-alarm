#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguagePublicationCodeRequiredMetadataTests
{
    [Fact]
    public void PublicationCode_is_marked_required()
    {
        var p = typeof(SectionLanguage).GetProperty(nameof(SectionLanguage.PublicationCode))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
