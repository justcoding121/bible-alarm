#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class FetchFlatPublicationTracksRequestTests
{
    [Fact]
    public async Task Init_Covers_Context_Publication_Format_And_Optional_Language()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var publication = new BiblePublication
        {
            Name = "Flat publication",
            PublicationCode = "FFPT-PUBLISH1",
            LanguageId = null,
            Language = null,
            IsVideo = true,
            IsMusic = false,
        };

        var lang = new Language
        {
            LanguageCode = "FFPT-LREQ",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        using var ctSource = new CancellationTokenSource();

        var sansLanguage = new FetchFlatPublicationTracksRequest
        {
            Db = db,
            NormalizedPublicationCode = "ffpt-flat",
            NormalizedLanguageCode = "*",
            EnglishPublication = publication,
            IsVideo = false,
            IsMusic = true,
            FileFormat = "AAC",
            Language = null,
            CancellationToken = ctSource.Token,
        };

        Assert.Same(db, sansLanguage.Db);
        Assert.Equal("ffpt-flat", sansLanguage.NormalizedPublicationCode);
        Assert.Equal("*", sansLanguage.NormalizedLanguageCode);
        Assert.Same(publication, sansLanguage.EnglishPublication);
        Assert.False(sansLanguage.IsVideo);
        Assert.True(sansLanguage.IsMusic);
        Assert.Equal("AAC", sansLanguage.FileFormat);
        Assert.Null(sansLanguage.Language);
        Assert.Equal(ctSource.Token, sansLanguage.CancellationToken);

        var withLanguage = new FetchFlatPublicationTracksRequest
        {
            Db = db,
            NormalizedPublicationCode = "dram",
            NormalizedLanguageCode = "E",
            EnglishPublication = publication,
            IsVideo = true,
            IsMusic = false,
            FileFormat = "MP4",
            Language = lang,
            CancellationToken = CancellationToken.None,
        };

        Assert.True(withLanguage.IsVideo);
        Assert.False(withLanguage.IsMusic);
        Assert.Equal("MP4", withLanguage.FileFormat);
        Assert.Same(lang, withLanguage.Language);
        Assert.Equal(CancellationToken.None, withLanguage.CancellationToken);
    }
}
