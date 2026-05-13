#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLanguageCategoryIdRequiredMetadataTests
{
    [Fact]
    public void CategoryId_is_marked_required()
    {
        var p = typeof(PublicationLanguage).GetProperty(nameof(PublicationLanguage.CategoryId))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
