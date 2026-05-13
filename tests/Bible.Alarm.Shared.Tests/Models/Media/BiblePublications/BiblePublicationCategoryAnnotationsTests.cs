#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationCategoryAnnotationsTests
{
    [Fact]
    public void Type_is_marked_with_composite_unique_index_on_publication_and_category()
    {
        var attr = Assert.Single(typeof(BiblePublicationCategory).GetCustomAttributes(typeof(IndexAttribute), inherit: false).Cast<IndexAttribute>());
        Assert.True(attr.IsUnique);
        Assert.Equal(
            new[] { nameof(BiblePublicationCategory.BiblePublicationId), nameof(BiblePublicationCategory.CategoryId) },
            attr.PropertyNames);
    }
}
