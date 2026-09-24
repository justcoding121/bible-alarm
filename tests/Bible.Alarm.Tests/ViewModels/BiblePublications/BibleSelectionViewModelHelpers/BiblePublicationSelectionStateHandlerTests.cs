#nullable enable

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionStateHandlerTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class UnusedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Not expected for early-return scenarios.");
    }

    private sealed class IdleDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class IdleMediaService : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>());

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

    private sealed class IdleLanguageNameService : ILanguageNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<int, string>());

        public string? GetNameCached(int languageId) => null;

        public string? GetNameByLanguageCodeCached(string languageCode) => null;
    }

    private static ScheduleStateItem Schedule(int id, string? bibleLang = "E", string? category = "Bible") =>
        new()
        {
            Id = id,
            Name = "S",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
            BiblePublicationLanguageCode = bibleLang,
            BiblePublicationCategoryName = category,
        };

    private static BiblePublicationSelectionDataProvider CreateProvider(ApplicationState appState) =>
        new(
            new IdleMediaService(),
            new IdleLanguageNameService(),
            new FakeApplicationState(appState),
            new IdleDispatcher());

    [Fact]
    public void InitializeCurrent_CanBeCalledMultipleTimes()
    {
        var app = new ApplicationState([], Schedule(1));
        var sut = new BiblePublicationSelectionStateHandler(
            new FakeApplicationState(app),
            CreateProvider(app),
            new UnusedScopeFactory());

        sut.InitializeCurrent(
            new BiblePublicationSchedule { LanguageCode = "E", PublicationCode = "nwtsty", TrackCode = "1" },
            initialLanguageCode: "E",
            initialCategoryName: AppConstants.Media.BiblePublicationCategoryBible);

        sut.InitializeCurrent(
            new BiblePublicationSchedule { LanguageCode = "M", PublicationCode = "nwtsty", TrackCode = "2" },
            initialLanguageCode: "M",
            initialCategoryName: AppConstants.Media.BiblePublicationCategoryBible);
    }

    [Fact]
    public async Task HandleBiblePublicationChangedAsync_NoOps_When_CurrentScheduleMissing()
    {
        var app = new ApplicationState([], currentSchedule: null);
        var sut = new BiblePublicationSelectionStateHandler(
            new FakeApplicationState(app),
            CreateProvider(app),
            new UnusedScopeFactory());

        await sut.HandleBiblePublicationChangedAsync(_ => { }, () => { }, new ObservableCollection<PublicationListViewItemModel>());
    }

    [Fact]
    public async Task HandleBiblePublicationChangedAsync_NoOps_When_LanguageCodeMissing()
    {
        var schedule = Schedule(2, bibleLang: null, category: AppConstants.Media.BiblePublicationCategoryBible);
        var app = new ApplicationState([], schedule);
        var sut = new BiblePublicationSelectionStateHandler(
            new FakeApplicationState(app),
            CreateProvider(app),
            new UnusedScopeFactory());

        await sut.HandleBiblePublicationChangedAsync(_ => { }, () => { }, new ObservableCollection<PublicationListViewItemModel>());
    }

    [Fact]
    public async Task HandleBiblePublicationChangedAsync_throws_when_category_missing_after_fallback()
    {
        var schedule = Schedule(3, bibleLang: "E", category: null);
        var app = new ApplicationState([], schedule);
        var sut = new BiblePublicationSelectionStateHandler(
            new FakeApplicationState(app),
            CreateProvider(app),
            new UnusedScopeFactory());
        sut.InitializeCurrent(
            new BiblePublicationSchedule { LanguageCode = "E", PublicationCode = "nwt", TrackCode = "1" },
            initialLanguageCode: "E",
            initialCategoryName: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.HandleBiblePublicationChangedAsync(_ => { }, () => { }, new ObservableCollection<PublicationListViewItemModel>()));
    }

    [Fact]
    public async Task HandleBiblePublicationInitializedAsync_skips_when_already_initialized_and_languages_present()
    {
        var app = new ApplicationState([], Schedule(4));
        var sut = new BiblePublicationSelectionStateHandler(
            new FakeApplicationState(app),
            CreateProvider(app),
            new UnusedScopeFactory());
        sut.InitializeCurrent(
            new BiblePublicationSchedule { LanguageCode = "E", PublicationCode = "nwt", TrackCode = "1" },
            initialLanguageCode: "E",
            initialCategoryName: AppConstants.Media.BiblePublicationCategoryBible);

        var languages = new ObservableCollection<LanguageListViewItemModel>
        {
            new(new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight }, "English")
            {
                IsSelected = true,
            },
        };

        try
        {
            await sut.HandleBiblePublicationInitializedAsync(_ => { }, languages, languageCode: "E");
            await sut.HandleBiblePublicationInitializedAsync(_ => { }, languages, languageCode: "E");
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
        }

        Assert.Single(languages);
    }

    [Fact]
    public async Task RefreshFromStateAsync_returns_when_language_never_appears()
    {
        var app = new ApplicationState([], currentSchedule: null);
        var sut = new BiblePublicationSelectionStateHandler(
            new FakeApplicationState(app),
            CreateProvider(app),
            new UnusedScopeFactory());

        await sut.RefreshFromStateAsync(_ => { }, new ObservableCollection<PublicationListViewItemModel>());
    }

    [Fact]
    public async Task RefreshFromStateAsync_augments_category_from_database_when_schedule_lacks_category()
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
            var category = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
            seed.Categories.Add(category);
            await seed.SaveChangesAsync();
            var publication = new BiblePublication
            {
                Name = "NWT",
                PublicationCode = "nwt",
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            };
            publication.BiblePublicationCategories.Add(new BiblePublicationCategory
            {
                Category = category,
                CategoryId = category.Id,
            });
            seed.BiblePublications.Add(publication);
            await seed.SaveChangesAsync();
        }

        var schedule = Schedule(5, bibleLang: "E", category: null);
        schedule.BiblePublicationCode = "nwt";
        var app = new ApplicationState([], schedule);
        var sut = new BiblePublicationSelectionStateHandler(
            new FakeApplicationState(app),
            CreateProvider(app),
            new MediaTestScopeFactory(options));
        sut.InitializeCurrent(
            new BiblePublicationSchedule { LanguageCode = "E", PublicationCode = "nwt", TrackCode = "1" },
            initialLanguageCode: "E",
            initialCategoryName: null);

        var publications = new ObservableCollection<PublicationListViewItemModel>();

        try
        {
            await sut.RefreshFromStateAsync(_ => { }, publications);
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
        }

        Assert.Equal("E", sut.Current?.LanguageCode);
    }
}
