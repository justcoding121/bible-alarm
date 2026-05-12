#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Services.Media.MediaServiceHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class MediaServiceTracksForNoLanguageHelperTests
{
    private static DbContextOptions<MediaDbContext> SqliteMemoryOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

    [Fact]
    public async Task GetTracksAsync_unknown_publication_returns_empty_sorted_dictionary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var result = await MediaServiceTracksForNoLanguageHelper.GetTracksAsync(factory, "missing-pub-code", null,
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTracksAsync_flat_publication_orders_by_track_code_key()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pub = "nlp-tracks";

        await using (var seed = new MediaDbContext(options))
        {
            var bp = new BiblePublication
            {
                Name = "No Lang Tracks",
                PublicationCode = pub,
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            };
            bp.Tracks.AddRange(
                new BiblePublicationTrack { TrackCode = "10", Title = "Ten", Publication = bp, BiblePublicationSectionId = null },
                new BiblePublicationTrack { TrackCode = "2", Title = "Two", Publication = bp, BiblePublicationSectionId = null });
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var result = await MediaServiceTracksForNoLanguageHelper.GetTracksAsync(factory, pub, sectionCode: null,
            CancellationToken.None);

        Assert.Equal(2, result.Count);

        var keys = result.Keys.ToList();
        Assert.Equal(new[] { "2", "10" }, keys);
    }
}
