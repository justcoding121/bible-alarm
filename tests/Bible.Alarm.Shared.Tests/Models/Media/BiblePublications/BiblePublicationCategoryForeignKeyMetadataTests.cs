#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationCategoryForeignKeyMetadataTests
{
    [Fact]
    public void Foreign_keys_reference_expected_navigations()
    {
        AssertFk(nameof(BiblePublicationCategory.BiblePublicationId), nameof(BiblePublicationCategory.BiblePublication));
        AssertFk(nameof(BiblePublicationCategory.CategoryId), nameof(BiblePublicationCategory.Category));
    }

    private static void AssertFk(string propertyName, string expectedNavigationName)
    {
        var fk = typeof(BiblePublicationCategory).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(expectedNavigationName, fk.Name);
    }
}
