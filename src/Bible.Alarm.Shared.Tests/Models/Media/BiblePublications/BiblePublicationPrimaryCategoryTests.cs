#nullable enable

using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationPrimaryCategoryTests
{
    private static BiblePublication Pub() =>
        new()
        {
            Id = 1,
            Name = "Book",
            PublicationCode = "nw",
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false,
            BiblePublicationCategories = [],
        };

    private static void AddCategory(BiblePublication pub, Category category)
    {
        pub.BiblePublicationCategories.Add(
            new BiblePublicationCategory
            {
                BiblePublicationId = pub.Id,
                BiblePublication = pub,
                CategoryId = category.Id,
                Category = category,
            });
    }

    [Fact]
    public void PrimaryCategory_IsNull_IdZero_WhenNoCategoriesLinked()
    {
        var pub = Pub();

        Assert.Null(pub.PrimaryCategory);
        Assert.Equal(0, pub.PrimaryCategoryId);
    }

    [Fact]
    public void PrimaryCategory_UsesFirstJunction_WhenOneLinked()
    {
        var pub = Pub();
        var bible = new Category { Id = 10, CategoryCode = "Bible" };
        AddCategory(pub, bible);

        Assert.Same(bible, pub.PrimaryCategory);
        Assert.Equal(10, pub.PrimaryCategoryId);
    }

    [Fact]
    public void PrimaryCategory_UsesIndexedFirst_NotOrdered_Key()
    {
        var pub = Pub();
        var second = new Category { Id = 2, CategoryCode = "Second" };
        var first = new Category { Id = 1, CategoryCode = "First" };

        AddCategory(pub, second);
        pub.BiblePublicationCategories.Insert(
            0,
            new BiblePublicationCategory
            {
                BiblePublicationId = pub.Id,
                BiblePublication = pub,
                CategoryId = first.Id,
                Category = first,
            });

        Assert.Same(first, pub.PrimaryCategory);
        Assert.Equal(1, pub.PrimaryCategoryId);
    }
}
