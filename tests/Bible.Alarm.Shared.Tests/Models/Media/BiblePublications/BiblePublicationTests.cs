#nullable enable

using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTests
{
    [Fact]
    public void PrimaryCategory_is_null_when_no_categories_linked()
    {
        var sut = new BiblePublication { BiblePublicationCategories = [] };

        Assert.Null(sut.PrimaryCategory);
    }

    [Fact]
    public void PrimaryCategoryId_is_zero_when_no_categories_linked()
    {
        var sut = new BiblePublication { BiblePublicationCategories = [] };

        Assert.Equal(0, sut.PrimaryCategoryId);
    }

    [Fact]
    public void PrimaryCategory_reflects_first_linked_category()
    {
        var category = new Category { Id = 1, CategoryCode = "Music" };
        var junction = new BiblePublicationCategory
        {
            BiblePublicationId = 1,
            CategoryId = 1,
            Category = category,
        };
        var sut = new BiblePublication
        {
            Id = 1,
            BiblePublicationCategories = [junction],
        };

        Assert.Same(category, sut.PrimaryCategory);
        Assert.Equal(1, sut.PrimaryCategoryId);
    }
}
