#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLanguageAnnotationsTests
{
    [Fact]
    public void Has_composite_unique_index_on_publication_language_and_category()
    {
        var idx = Assert.Single(
            typeof(PublicationLanguage).GetCustomAttributes(typeof(IndexAttribute), inherit: false).Cast<IndexAttribute>());

        Assert.True(idx.IsUnique);
        Assert.Equal(
            new[]
            {
                nameof(PublicationLanguage.PublicationCode),
                nameof(PublicationLanguage.LanguageId),
                nameof(PublicationLanguage.CategoryId),
            },
            idx.PropertyNames);
    }
}
