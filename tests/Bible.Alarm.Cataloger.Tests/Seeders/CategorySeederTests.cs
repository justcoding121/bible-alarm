#nullable enable

using Bible.Alarm.Cataloger.Seeders;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class CategorySeederTests
{
    private static Serilog.Core.Logger QuietLogger =>
        new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    private static MediaDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MediaDbContext(opts);
    }

    [Fact]
    public async Task SeedDefault_categories_inserts_expected_distinct_codes()
    {
        await using var db = CreateDb();

        await new CategorySeeder(QuietLogger).SeedDefaultCategoriesAndApiUrls(db);

        var codes = db.Categories.Select(c => c.CategoryCode).OrderBy(x => x).ToList();

        Assert.Equal(20, codes.Count);
        Assert.Contains(AppConstants.Media.BiblePublicationCategoryBible, codes);
        Assert.Contains(AppConstants.Media.BiblePublicationCategoryAwakeMagazine, codes);
        Assert.Single(codes, c => c == AppConstants.Media.BiblePublicationCategoryBible);
    }

    [Fact]
    public async Task Seed_default_is_idempotent()
    {
        await using var db = CreateDb();
        var sut = new CategorySeeder(QuietLogger);

        await sut.SeedDefaultCategoriesAndApiUrls(db);
        var first = db.Categories.Count();

        await sut.SeedDefaultCategoriesAndApiUrls(db);
        Assert.Equal(first, db.Categories.Count());
    }

    [Fact]
    public async Task Skips_codes_already_present_when_persisted()
    {
        await using var db = CreateDb();
        db.Categories.Add(new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible });
        await db.SaveChangesAsync();

        await new CategorySeeder(QuietLogger).SeedDefaultCategoriesAndApiUrls(db);

        Assert.Equal(
            1,
            db.Categories.Count(c => c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible));

        Assert.Equal(20, await db.Categories.CountAsync());
    }

    [Fact]
    public void Constructor_requires_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new CategorySeeder(null!));
    }
}
