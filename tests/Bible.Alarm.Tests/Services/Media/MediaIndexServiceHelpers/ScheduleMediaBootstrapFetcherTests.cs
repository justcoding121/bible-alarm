#nullable enable

using Bible.Alarm.Services.Media.MediaIndexServiceHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class ScheduleMediaBootstrapFetcherTests
{
    private sealed class RecordingLanguageContentService : ILanguageContentService
    {
        public List<(string Pub, string Lang)> EnsurePublicationCalls { get; } = [];
        public List<(string Pub, string Section, string Lang)> FetchSectionTracksCalls { get; } = [];

        public Task<bool> EnsurePublicationExistsAsync(
            string publicationCode,
            string languageCode,
            IFetchProgress? progress,
            CancellationToken cancellationToken)
        {
            EnsurePublicationCalls.Add((publicationCode, languageCode));
            return Task.FromResult(true);
        }

        public Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> FetchPublicationSectionsAsync(
            string publicationCode,
            string languageCode,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> FetchSectionTracksAsync(
            string publicationCode,
            string sectionCode,
            string languageCode,
            bool replaceExistingTracksFromApi,
            CancellationToken cancellationToken)
        {
            FetchSectionTracksCalls.Add((publicationCode, sectionCode, languageCode));
            return Task.FromResult(true);
        }

        public Task<bool> FetchFirstSectionOnlyAsync(
            string publicationCode,
            string firstSectionCode,
            string languageCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> FetchFirstPublicationForLanguageAsync(
            string languageCode,
            string? categoryName,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(
            string languageCode,
            string? categoryName,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> EnsureAllSectionsForPublicationAsync(
            string publicationCode,
            string languageCode,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<string?> GetVideoPublicationDisplayNameAsync(
            string publicationCode,
            string languageCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new ScheduleMediaBootstrapFetcher(
            TestLogging.CreateLogger(),
            null!,
            null!);

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task ReadNonEnglishSpanishReferencesAsync_throws_when_schedule_db_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_missing-schedule.db");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            ScheduleMediaBootstrapFetcher.ReadNonEnglishSpanishReferencesAsync(missing));
    }

    [Fact]
    public async Task ReadNonEnglishSpanishReferencesAsync_excludes_english_and_spanish()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);

            var refs = await ScheduleMediaBootstrapFetcher.ReadNonEnglishSpanishReferencesAsync(schedulePath);

            var single = Assert.Single(refs);
            Assert.Equal(MediaBootstrapTestDatabaseHelper.SamplePublicationCode, single.PublicationCode);
            Assert.Equal(MediaBootstrapTestDatabaseHelper.FrenchLanguageCode, single.LanguageCode);
            Assert.Equal(MediaBootstrapTestDatabaseHelper.SampleSectionCode, single.SectionCode);
            Assert.Equal(MediaBootstrapTestDatabaseHelper.SampleTrackCode, single.TrackCode);
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task FilterToValidPubAndSectionInDiscoveryAsync_keeps_refs_with_discovery_rows()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(mediaPath);

            var input =
                new List<ScheduleMediaBootstrapFetcher.ScheduleMediaReference>
                {
                    new(
                        MediaBootstrapTestDatabaseHelper.SamplePublicationCode,
                        MediaBootstrapTestDatabaseHelper.FrenchLanguageCode,
                        MediaBootstrapTestDatabaseHelper.SampleSectionCode,
                        MediaBootstrapTestDatabaseHelper.SampleTrackCode),
                    new("unknown", MediaBootstrapTestDatabaseHelper.FrenchLanguageCode, "x", "1"),
                };

            var filtered = await ScheduleMediaBootstrapFetcher.FilterToValidPubAndSectionInDiscoveryAsync(mediaPath, input);

            var single = Assert.Single(filtered);
            Assert.Equal(MediaBootstrapTestDatabaseHelper.SamplePublicationCode, single.PublicationCode);
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task PublicationExistsInDiscoveryAsync_returns_true_when_row_present()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(mediaPath);

            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={mediaPath};Mode=ReadOnly");
            await connection.OpenAsync();

            Assert.True(await ScheduleMediaBootstrapFetcher.PublicationExistsInDiscoveryAsync(
                connection,
                MediaBootstrapTestDatabaseHelper.SamplePublicationCode,
                MediaBootstrapTestDatabaseHelper.FrenchLanguageCode));
            Assert.False(await ScheduleMediaBootstrapFetcher.PublicationExistsInDiscoveryAsync(
                connection,
                "missing-pub",
                MediaBootstrapTestDatabaseHelper.FrenchLanguageCode));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task SectionExistsInDiscoveryAsync_returns_true_when_row_present()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(mediaPath);

            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={mediaPath};Mode=ReadOnly");
            await connection.OpenAsync();

            Assert.True(await ScheduleMediaBootstrapFetcher.SectionExistsInDiscoveryAsync(
                connection,
                MediaBootstrapTestDatabaseHelper.SamplePublicationCode,
                MediaBootstrapTestDatabaseHelper.SampleSectionCode,
                MediaBootstrapTestDatabaseHelper.FrenchLanguageCode));
            Assert.False(await ScheduleMediaBootstrapFetcher.SectionExistsInDiscoveryAsync(
                connection,
                MediaBootstrapTestDatabaseHelper.SamplePublicationCode,
                "missing-section",
                MediaBootstrapTestDatabaseHelper.FrenchLanguageCode));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task FetchMissingAsync_noops_when_schedule_db_missing()
    {
        var languageContent = new RecordingLanguageContentService();
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var sut = new ScheduleMediaBootstrapFetcher(
            TestLogging.CreateLogger(),
            languageContent,
            new MediaTestScopeFactory(options));

        await sut.FetchMissingAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_missing.db"));

        Assert.Empty(languageContent.EnsurePublicationCalls);
    }

    [Fact]
    public async Task FetchMissingAsync_ensures_publication_and_fetches_section_for_valid_refs()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(mediaPath);

            var languageContent = new RecordingLanguageContentService();
            var mediaOptions = new DbContextOptionsBuilder<MediaDbContext>()
                .UseSqlite($"Data Source={mediaPath}")
                .Options;
            var scopeFactory = new MediaTestScopeFactory(mediaOptions);
            var sut = new ScheduleMediaBootstrapFetcher(
                TestLogging.CreateLogger(),
                languageContent,
                scopeFactory);

            await sut.FetchMissingAsync(schedulePath, mediaPath);

            Assert.Equal(
                (MediaBootstrapTestDatabaseHelper.SamplePublicationCode, MediaBootstrapTestDatabaseHelper.FrenchLanguageCode),
                Assert.Single(languageContent.EnsurePublicationCalls));
            Assert.Equal(
                (MediaBootstrapTestDatabaseHelper.SamplePublicationCode,
                    MediaBootstrapTestDatabaseHelper.SampleSectionCode,
                    MediaBootstrapTestDatabaseHelper.FrenchLanguageCode),
                Assert.Single(languageContent.FetchSectionTracksCalls));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }
}
