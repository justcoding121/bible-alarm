#nullable enable

using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Tests.Support;

namespace Bible.Alarm.Shared.Tests;

public sealed class CategoryNameServiceTests
{
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CategoryNameService(null!));
    }

    [Fact]
    public async Task GetName_ReturnsNull_WhenCategoryCodeBlank()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("E");

        Assert.Null(sut.GetName(null!, "E"));
        Assert.Null(sut.GetName("", "E"));
        Assert.Null(sut.GetName(" ", "E"));
        Assert.Null(sut.GetName("\t", "E"));
    }

    [Fact]
    public void GetName_ReturnsNull_WhenWarmCacheNeverInvoked()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        Assert.Null(sut.GetName("Bible", "E"));
    }

    [Fact]
    public async Task GetName_ReturnsNull_WhenDisplayLanguageMismatch()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("E");

        Assert.Null(sut.GetName("Bible", "F"));
    }

    [Fact]
    public async Task GetName_ReturnsNull_WhenDisplayLanguageCodeHasPadding_NotEqualityMatch()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("E");

        Assert.Null(sut.GetName("Bible", " E "));
    }

    [Fact]
    public async Task WarmThenGet_ReadsEmbeddedEnglishCategories()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("E");

        Assert.Equal("Bible", sut.GetName("Bible", "E"));
        Assert.Equal("Faith and Bible", sut.GetName("FaithAndBible", "E"));
        Assert.Equal("The Watchtower (Magazine)", sut.GetName("WatchtowerMagazine", "E"));
    }

    [Fact]
    public async Task WarmCache_WithMissingResource_EmptiesLookup()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("__NoSuchBundle__");

        Assert.Null(sut.GetName("Bible", "__NoSuchBundle__"));
    }

    [Fact]
    public async Task GetName_DisplayLanguageComparison_IsInsensitive()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("E");

        Assert.Equal("Music", sut.GetName("Music", "e"));
    }

    [Fact]
    public async Task GetName_CategoryLookup_IsInsensitiveToKeyCasing_FromEmbeddedEnglish()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("E");

        Assert.Equal("Bible", sut.GetName("bible", "E"));
        Assert.Equal("Faith and Bible", sut.GetName("faithandbible", "E"));
    }

    [Fact]
    public async Task GetName_ReturnsNull_WhenCategoryCodeAbsentFromWarmBundle()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("E");

        Assert.Null(sut.GetName("NonexistentCatalogCategory", "E"));
    }

    [Fact]
    public async Task WarmCache_Overwrites_PreviousDisplayLanguage_Context()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("E");
        Assert.Equal("Books", sut.GetName("Books", "E"));

        await sut.WarmCacheForDisplayLanguageAsync("__NoSuchBundle__");

        Assert.Null(sut.GetName("Books", "E"));
        Assert.Null(sut.GetName("Books", "__NoSuchBundle__"));
    }

    [Fact]
    public async Task WarmCache_IgnoresBlankDisplayLanguageCodes()
    {
        using var logger = TestLogging.CreateLogger();
        var sut = new CategoryNameService(logger);

        await sut.WarmCacheForDisplayLanguageAsync("");
        await sut.WarmCacheForDisplayLanguageAsync("   ");

        Assert.Null(sut.GetName("Bible", "E"));
    }
}
