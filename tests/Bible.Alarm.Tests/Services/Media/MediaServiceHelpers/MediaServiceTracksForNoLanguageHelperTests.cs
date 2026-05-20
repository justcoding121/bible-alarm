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

    [Fact]
    public async Task GetTracksAsync_flat_publication_without_tracks_returns_empty_sorted_dictionary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pub = "nlp-empty-flat";

        await using (var seed = new MediaDbContext(options))
        {
            seed.BiblePublications.Add(new BiblePublication
            {
                Name = "Empty flat",
                PublicationCode = pub,
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            });
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var result = await MediaServiceTracksForNoLanguageHelper.GetTracksAsync(factory, pub, sectionCode: null,
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTracksAsync_whitespace_section_code_uses_flat_track_query()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pub = "nlp-whitespace-section";

        await using (var seed = new MediaDbContext(options))
        {
            var bp = new BiblePublication
            {
                Name = "Whitespace section",
                PublicationCode = pub,
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            };
            bp.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "1",
                Title = "One",
                Publication = bp,
                BiblePublicationSectionId = null,
            });
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var result = await MediaServiceTracksForNoLanguageHelper.GetTracksAsync(factory, pub, sectionCode: "   ",
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("1", result.Keys.First());
    }

    [Fact]
    public async Task GetTracksAsync_section_with_tracks_orders_by_track_code()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pub = "nlp-section-tracks";
        const string section = "ch1";

        await using (var seed = new MediaDbContext(options))
        {
            var bp = new BiblePublication
            {
                Name = "Section tracks",
                PublicationCode = pub,
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            };
            var sec = new BiblePublicationSection { SectionCode = section, BiblePublication = bp };
            bp.Sections.Add(sec);
            bp.Tracks.AddRange(
                new BiblePublicationTrack { TrackCode = "10", Title = "Ten", Publication = bp, Section = sec },
                new BiblePublicationTrack { TrackCode = "2", Title = "Two", Publication = bp, Section = sec });
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var result = await MediaServiceTracksForNoLanguageHelper.GetTracksAsync(factory, pub, section,
            CancellationToken.None);

        Assert.Equal(new[] { "2", "10" }, result.Keys.ToList());
    }

    [Fact]
    public async Task GetTracksAsync_unknown_section_returns_empty_sorted_dictionary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pub = "nlp-missing-section";

        await using (var seed = new MediaDbContext(options))
        {
            seed.BiblePublications.Add(new BiblePublication
            {
                Name = "No section",
                PublicationCode = pub,
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            });
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var result = await MediaServiceTracksForNoLanguageHelper.GetTracksAsync(factory, pub, "missing",
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTracksAsync_section_without_tracks_returns_empty_sorted_dictionary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pub = "nlp-empty-section";
        const string section = "empty";

        await using (var seed = new MediaDbContext(options))
        {
            var bp = new BiblePublication
            {
                Name = "Empty section",
                PublicationCode = pub,
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            };
            bp.Sections.Add(new BiblePublicationSection { SectionCode = section, BiblePublication = bp });
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var result = await MediaServiceTracksForNoLanguageHelper.GetTracksAsync(factory, pub, section,
            CancellationToken.None);

        Assert.Empty(result);
    }
}
