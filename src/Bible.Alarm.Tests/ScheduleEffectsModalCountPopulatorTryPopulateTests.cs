#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEffectsModalCountPopulatorTryPopulateTests
{
    private sealed class ModalCountTestScopeFactory(DbContextOptions<MediaDbContext> options, IMediaService media) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope(options, media);

        private sealed class Scope(DbContextOptions<MediaDbContext> options, IMediaService media) : IServiceScope, IDisposable
        {
            private readonly MediaDbContext db = new(options);

            public IServiceProvider ServiceProvider => new Provider(db, media);

            public void Dispose() => db.Dispose();

            private sealed class Provider(MediaDbContext db, IMediaService media) : IServiceProvider
            {
                public object? GetService(Type serviceType) =>
                    serviceType == typeof(MediaDbContext) ? db :
                    serviceType == typeof(IMediaService) ? media : null;
            }
        }
    }

    private sealed class EmptyTracksMediaService : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    private sealed class BoomScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("scope unavailable");
    }

    [Fact]
    public async Task TryPopulateModalCountsAsync_preserves_music_counts_when_music_disabled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        var scopeFactory = new ModalCountTestScopeFactory(options, new EmptyTracksMediaService());
        var current = new ScheduleStateItem
        {
            Id = 7,
            BiblePublicationCategoryName = "Bible",
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "",
            MusicEnabled = false,
            MusicPublicationModalItemCount = 11,
            MusicSectionModalItemCount = 3,
        };

        var updated = await ScheduleEffectsModalCountPopulator.TryPopulateModalCountsAsync(
            current,
            scopeFactory,
            TestLogging.CreateLogger());

        Assert.NotNull(updated);
        Assert.Equal(0, updated.BiblePublicationModalItemCount);
        Assert.Equal(0, updated.BiblePublicationSectionModalItemCount);
        Assert.Null(updated.BiblePublicationTrackModalItemCount);
        Assert.Equal(11, updated.MusicPublicationModalItemCount);
        Assert.Equal(3, updated.MusicSectionModalItemCount);
    }

    [Fact]
    public async Task TryPopulateModalCountsAsync_populates_music_modal_counts_when_music_enabled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Categories.Add(musicCat);
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            var pl = new PublicationLanguage
            {
                PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                Category = musicCat,
                Language = lang,
                IsMusic = true,
            };
            seed.PublicationLanguages.Add(pl);
            await seed.SaveChangesAsync();

            seed.SectionLanguages.AddRange(
                new SectionLanguage
                {
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    SectionCode = "iam-x",
                    Language = lang,
                    PublicationLanguage = pl,
                },
                new SectionLanguage
                {
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    SectionCode = "iam-y",
                    Language = lang,
                    PublicationLanguage = pl,
                });

            await seed.SaveChangesAsync();
        }

        var scopeFactory = new ModalCountTestScopeFactory(options, new EmptyTracksMediaService());
        var current = new ScheduleStateItem
        {
            Id = 8,
            BiblePublicationCategoryName = null,
            BiblePublicationLanguageCode = null,
            BiblePublicationCode = "",
            MusicEnabled = true,
            MusicPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            MusicLanguageCode = AppConstants.Media.DefaultLanguageCode,
        };

        var updated = await ScheduleEffectsModalCountPopulator.TryPopulateModalCountsAsync(
            current,
            scopeFactory,
            TestLogging.CreateLogger());

        Assert.NotNull(updated);
        Assert.Null(updated.BiblePublicationModalItemCount);
        Assert.Equal(1, updated.MusicPublicationModalItemCount);
        Assert.Equal(2, updated.MusicSectionModalItemCount);
    }

    [Fact]
    public async Task TryPopulateModalCountsAsync_returns_null_when_scope_factory_fails()
    {
        var current = new ScheduleStateItem { Id = 3, MusicEnabled = false };

        var result = await ScheduleEffectsModalCountPopulator.TryPopulateModalCountsAsync(
            current,
            new BoomScopeFactory(),
            TestLogging.CreateLogger());

        Assert.Null(result);
    }
}
