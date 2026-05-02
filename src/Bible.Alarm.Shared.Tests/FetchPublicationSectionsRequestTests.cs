#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class FetchPublicationSectionsRequestTests
{
    private sealed class NoOpFetchProgress(CancellationToken token) : IFetchProgress
    {
        public CancellationToken CancellationToken => token;

        public void UpdateProgress(double progress) { }

        public void UpdateProgressText(string text) { }

        public void SetIsVisible(bool isVisible) { }
    }

    [Fact]
    public async Task Init_Captures_Core_Fields_And_Optional_Progress()
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
            Name = "Fetch PS Request",
            PublicationCode = "FPSR-PUB-01",
            LanguageId = null,
            Language = null,
            IsVideo = false,
            IsMusic = false,
        };

        List<string> sectionCodes = ["mat-1", "exo-40"];
        using var ctSource = new CancellationTokenSource();

        var withoutProgress = new FetchPublicationSectionsRequest
        {
            Db = db,
            NormalizedPublicationCode = "fpsr-normal",
            NormalizedLanguageCode = "en",
            PublicationCodeForDb = "FPSR-PUB-01",
            EnglishPublication = publication,
            SectionCodes = sectionCodes,
            CancellationToken = ctSource.Token,
        };

        Assert.Same(db, withoutProgress.Db);
        Assert.Equal("fpsr-normal", withoutProgress.NormalizedPublicationCode);
        Assert.Equal("en", withoutProgress.NormalizedLanguageCode);
        Assert.Equal("FPSR-PUB-01", withoutProgress.PublicationCodeForDb);
        Assert.Same(publication, withoutProgress.EnglishPublication);
        Assert.Same(sectionCodes, withoutProgress.SectionCodes);
        Assert.Equal(ctSource.Token, withoutProgress.CancellationToken);
        Assert.Null(withoutProgress.Progress);

        var progress = new NoOpFetchProgress(ctSource.Token);
        var withProgress = new FetchPublicationSectionsRequest
        {
            Db = db,
            NormalizedPublicationCode = "x",
            NormalizedLanguageCode = "y",
            PublicationCodeForDb = "FPSR-PUB-01",
            EnglishPublication = publication,
            SectionCodes = sectionCodes,
            CancellationToken = CancellationToken.None,
            Progress = progress,
        };

        Assert.Same(progress, withProgress.Progress);
        Assert.Equal(CancellationToken.None, withProgress.CancellationToken);
    }
}
