#nullable enable

using Bible.Alarm.Cataloger.Seeders;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class LanguageSeederTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    private static MediaDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MediaDbContext(opts);
    }

    private static string BuildLanguagesJson(params (string Code, string Name, bool IsSignLanguage, string Direction)[] entries)
    {
        var lc = AppConstants.Media.LanguageIndexJson.LangCode;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var sign = AppConstants.Media.LanguageIndexJson.IsSignLanguage;
        var dir = AppConstants.Media.LanguageIndexJson.Direction;
        var lng = AppConstants.Media.LanguageIndexJson.Languages;

        var items = string.Join(
            ",",
            entries.Select(e =>
                $$"""{"{{lc}}":"{{e.Code}}","{{nm}}":"{{e.Name}}","{{sign}}":{{e.IsSignLanguage.ToString().ToLowerInvariant()}},"{{dir}}":"{{e.Direction}}"}"""));

        return $$"""{"{{lng}}":[{{items}}]}""";
    }

    [Fact]
    public void Constructor_requires_logger()
    {
        var downloadUtility = new StubDownloadUtility(SilentLogger, _ => Task.FromResult("{}"));

        Assert.Throws<ArgumentNullException>(() => new LanguageSeeder(null!, downloadUtility));
    }

    [Fact]
    public void Constructor_requires_download_utility()
    {
        Assert.Throws<ArgumentNullException>(() => new LanguageSeeder(SilentLogger, null!));
    }

    [Fact]
    public async Task GetOrCreateLanguageByCode_creates_language_from_stubbed_jw_org_response()
    {
        await using var db = CreateDb();
        var json = BuildLanguagesJson(("E", "English", false, "ltr"));
        var downloadUtility = new StubDownloadUtility(SilentLogger, _ => Task.FromResult(json));
        var sut = new LanguageSeeder(SilentLogger, downloadUtility);

        var language = await sut.GetOrCreateLanguageByCode(db, AppConstants.Media.DefaultLanguageCode);

        Assert.Equal(AppConstants.Media.DefaultLanguageCode, language.LanguageCode);
        Assert.Equal("ltr", language.Direction);
        Assert.Single(await db.Languages.ToListAsync());
        var displayName = await db.LanguageNamesByLanguage.SingleAsync();
        Assert.Equal("English", displayName.Name);
    }

    [Fact]
    public async Task GetOrCreateLanguageByCode_returns_existing_language_without_download()
    {
        await using var db = CreateDb();
        db.Languages.Add(new Language
        {
            LanguageCode = AppConstants.Media.DefaultLanguageCode,
            Direction = "ltr",
        });
        await db.SaveChangesAsync();

        var downloadUtility = new StubDownloadUtility(SilentLogger, _ =>
            throw new InvalidOperationException("Download should not be invoked when language already exists."));
        var sut = new LanguageSeeder(SilentLogger, downloadUtility);

        var language = await sut.GetOrCreateLanguageByCode(db, AppConstants.Media.DefaultLanguageCode);

        Assert.Equal(AppConstants.Media.DefaultLanguageCode, language.LanguageCode);
        Assert.Empty(downloadUtility.RequestedUrls);
    }

    [Fact]
    public async Task FetchLanguageInfoFromJwOrgLanguagesApi_returns_name_and_direction()
    {
        var json = BuildLanguagesJson(("S", "Spanish&nbsp;Name", false, "rtl"));
        var downloadUtility = new StubDownloadUtility(SilentLogger, _ => Task.FromResult(json));
        var sut = new LanguageSeeder(SilentLogger, downloadUtility);

        var (name, direction) = await sut.FetchLanguageInfoFromJwOrgLanguagesApi("S");

        Assert.Equal("Spanish Name", name);
        Assert.Equal("rtl", direction);
        Assert.Single(downloadUtility.RequestedUrls);
        Assert.Equal(AppConstants.ApiEndpoints.JwOrgLanguagesListUrl, downloadUtility.RequestedUrls[0]);
    }

    [Fact]
    public async Task SeedAllLanguagesFromJwOrg_skips_sign_languages_and_seeds_new_entries()
    {
        await using var db = CreateDb();
        var json = BuildLanguagesJson(
            ("E", "English", false, "ltr"),
            ("ASL", "American Sign Language", true, "ltr"),
            ("MY", "Malayalam", false, "ltr"));
        var downloadUtility = new StubDownloadUtility(SilentLogger, _ => Task.FromResult(json));
        var sut = new LanguageSeeder(SilentLogger, downloadUtility);

        await sut.SeedAllLanguagesFromJwOrg(db);

        var codes = await db.Languages.Select(l => l.LanguageCode).OrderBy(c => c).ToListAsync();
        Assert.Equal(["E", "MY"], codes);
        Assert.Equal(2, await db.LanguageNamesByLanguage.CountAsync());
    }

    [Fact]
    public async Task SeedAllLanguagesFromJwOrg_is_idempotent_for_existing_language_codes()
    {
        await using var db = CreateDb();
        db.Languages.Add(new Language { LanguageCode = "E", Direction = "ltr" });
        await db.SaveChangesAsync();

        var json = BuildLanguagesJson(("E", "English", false, "ltr"), ("MY", "Malayalam", false, "ltr"));
        var downloadUtility = new StubDownloadUtility(SilentLogger, _ => Task.FromResult(json));
        var sut = new LanguageSeeder(SilentLogger, downloadUtility);

        await sut.SeedAllLanguagesFromJwOrg(db);

        Assert.Equal(2, await db.Languages.CountAsync());
        Assert.Single(await db.Languages.Where(l => l.LanguageCode == "MY").ToListAsync());
    }
}
