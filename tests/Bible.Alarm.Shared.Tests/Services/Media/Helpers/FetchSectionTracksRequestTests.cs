#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class FetchSectionTracksRequestTests
{
    [Fact]
    public async Task Init_Holds_Context_Publication_Section_And_ReplaceExisting()
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
            Name = "Section tracks pub",
            PublicationCode = "FSTR-PUB-X1",
            LanguageId = null,
            Language = null,
            IsVideo = false,
            IsMusic = false,
        };

        var section = new BiblePublicationSection
        {
            Name = "Matthew",
            SectionCode = "mat-88",
            BiblePublication = publication,
        };

        publication.Sections.Add(section);

        using var cts = new CancellationTokenSource();

        var baseline = new FetchSectionTracksRequest
        {
            Db = db,
            NormalizedPublicationCode = "fst-p",
            NormalizedSectionCode = "mat-77",
            NormalizedLanguageCode = "en-gr",
            PublicationCodeForDb = "FSTR-PUB-X1",
            Publication = publication,
            Section = section,
            CancellationToken = cts.Token,
            ReplaceExisting = false,
        };

        Assert.Same(db, baseline.Db);
        Assert.Equal("fst-p", baseline.NormalizedPublicationCode);
        Assert.Equal("mat-77", baseline.NormalizedSectionCode);
        Assert.Equal("en-gr", baseline.NormalizedLanguageCode);
        Assert.Equal("FSTR-PUB-X1", baseline.PublicationCodeForDb);
        Assert.Same(publication, baseline.Publication);
        Assert.Same(section, baseline.Section);
        Assert.Equal(cts.Token, baseline.CancellationToken);
        Assert.False(baseline.ReplaceExisting);

        var overwrite = new FetchSectionTracksRequest
        {
            Db = db,
            NormalizedPublicationCode = "fst-p",
            NormalizedSectionCode = "mat-77",
            NormalizedLanguageCode = "en-gr",
            PublicationCodeForDb = "FSTR-PUB-X1",
            Publication = publication,
            Section = section,
            CancellationToken = CancellationToken.None,
            ReplaceExisting = true,
        };

        Assert.True(overwrite.ReplaceExisting);
        Assert.Equal(CancellationToken.None, overwrite.CancellationToken);
    }
}
