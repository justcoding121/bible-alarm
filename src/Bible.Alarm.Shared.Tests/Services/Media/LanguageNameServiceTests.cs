#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameServiceTests : IAsyncLifetime
{
    private static readonly int[] SingleLanguageId = [1];

    private readonly SqliteConnection connection = new("Data Source=:memory:");

    private DbContextOptions<MediaDbContext> Options =>
        new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

    public Task InitializeAsync()
    {
        connection.Open();
        using var bootstrap = new MediaDbContext(Options);
        bootstrap.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => connection.DisposeAsync().AsTask();

    [Fact]
    public void Constructor_ThrowsWhenScopeFactoryNull()
        => Assert.Throws<ArgumentNullException>(() =>
            new LanguageNameService(null!, TestLogging.CreateLogger()));

    [Fact]
    public void Constructor_ThrowsWhenLoggerNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LanguageNameService(new MediaTestScopeFactory(Options), null!));
    }

    [Fact]
    public async Task GetNameAsync_ReturnsNull_WhenDisplayLanguageBlank()
    {
        await SeedSingleLanguageAsync(languageCode: "E", englishName: "English");
        var sut = CreateSut();

        Assert.Null(await sut.GetNameAsync(1, ""));
        Assert.Null(await sut.GetNameAsync(1, "   "));
    }

    [Fact]
    public async Task GetNameByLanguageCode_ReturnsNull_WhenLanguageCodeOrDisplayBlank()
    {
        await SeedSingleLanguageAsync(languageCode: "E", englishName: "English");
        var sut = CreateSut();

        Assert.Null(await sut.GetNameByLanguageCodeAsync("", "E"));
        Assert.Null(await sut.GetNameByLanguageCodeAsync("E", ""));
    }

    [Fact]
    public async Task GetNameAsync_HitsDatabase_WhenCacheCold()
    {
        await SeedSingleLanguageAsync(languageCode: "FF", englishName: "Faroese Friendly");
        var sut = CreateSut();

        Assert.Equal(
            "Faroese Friendly",
            await sut.GetNameAsync(languageId: 1, displayLanguageCode: "E"));

        Assert.Equal(
            "Faroese Friendly",
            await sut.GetNameByLanguageCodeAsync(languageCode: "FF", displayLanguageCode: "E"));
    }

    [Fact]
    public async Task WarmThenCachedLookups_UseWarmedMaps()
    {
        await SeedSingleLanguageAsync(languageCode: "SGN", englishName: "Sign English");
        var sut = CreateSut();

        await sut.WarmCacheForDisplayLanguageAsync("E");

        Assert.Equal("Sign English", sut.GetNameCached(1));
        Assert.Equal("Sign English", sut.GetNameByLanguageCodeCached("sgn"));

        Assert.Equal("Sign English", await sut.GetNameAsync(1, "e"));
        Assert.Equal("Sign English", await sut.GetNameByLanguageCodeAsync("SGN", "e"));
    }

    [Fact]
    public async Task GetNamesAsync_ReturnsSubset_AndIgnoresMissingIds()
    {
        await using var db = new MediaDbContext(Options);

        var l1 = new Language { LanguageCode = "AA", Direction = AppConstants.Media.TextDirectionLeftToRight };
        db.Languages.Add(l1);
        await db.SaveChangesAsync();

        db.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
        {
            LanguageId = l1.Id,
            DisplayLanguageCode = "E",
            Name = "Alpha",
            Language = l1,
        });

        var l2 = new Language { LanguageCode = "BB", Direction = AppConstants.Media.TextDirectionLeftToRight };
        db.Languages.Add(l2);
        await db.SaveChangesAsync();

        db.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
        {
            LanguageId = l2.Id,
            DisplayLanguageCode = "E",
            Name = "Bravo",
            Language = l2,
        });

        await db.SaveChangesAsync();

        var sut = CreateSut();
        await sut.WarmCacheForDisplayLanguageAsync("E");

        var map = await sut.GetNamesAsync(new[] { l1.Id, l2.Id, 999 }, "E");

        Assert.Equal(2, map.Count);
        Assert.Equal("Alpha", map[l1.Id]);
        Assert.Equal("Bravo", map[l2.Id]);
        Assert.False(map.ContainsKey(999));
    }

    [Fact]
    public async Task GetNamesAsync_ReturnsEmpty_ForBlankDisplayLanguageOrEmptyIdList()
    {
        await SeedSingleLanguageAsync(languageCode: "E", englishName: "English");
        var sut = CreateSut();

        Assert.Empty(await sut.GetNamesAsync(SingleLanguageId, ""));
        Assert.Empty(await sut.GetNamesAsync(Array.Empty<int>(), "E"));
    }

    [Fact]
    public async Task WarmCache_IgnoresBlank_DisplayLanguageCodes()
    {
        await SeedSingleLanguageAsync(languageCode: "JP", englishName: "Japanese");
        var sut = CreateSut();

        await sut.WarmCacheForDisplayLanguageAsync("");
        await sut.WarmCacheForDisplayLanguageAsync("   ");

        Assert.Null(sut.GetNameCached(1));
    }

    private LanguageNameService CreateSut() =>
        new(new MediaTestScopeFactory(Options), TestLogging.CreateLogger());

    private async Task SeedSingleLanguageAsync(string languageCode, string englishName)
    {
        await using var db = new MediaDbContext(Options);
        var lang = new Language
        {
            LanguageCode = languageCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        db.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
        {
            LanguageId = lang.Id,
            DisplayLanguageCode = "E",
            Name = englishName,
            Language = lang,
        });

        await db.SaveChangesAsync();
    }
}
