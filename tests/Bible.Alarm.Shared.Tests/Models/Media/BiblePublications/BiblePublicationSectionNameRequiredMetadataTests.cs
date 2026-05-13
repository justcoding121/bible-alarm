#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionNameRequiredMetadataTests
{
    [Fact]
    public void Name_is_marked_required()
    {
        var p = typeof(BiblePublicationSection).GetProperty(nameof(BiblePublicationSection.Name))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
