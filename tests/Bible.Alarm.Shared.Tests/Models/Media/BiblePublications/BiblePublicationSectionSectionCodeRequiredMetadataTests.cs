#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionSectionCodeRequiredMetadataTests
{
    [Fact]
    public void SectionCode_is_marked_required()
    {
        var p = typeof(BiblePublicationSection).GetProperty(nameof(BiblePublicationSection.SectionCode))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
