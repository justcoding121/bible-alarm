#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecordingDispatcher = Bible.Alarm.Tests.Support.ViewModelTestDoubles.RecordingDispatcher;
using MutableApplicationState = Bible.Alarm.Tests.Support.ViewModelTestDoubles.MutableApplicationState;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicCommandInitializerTests
{
    private sealed class RecordingToastService : IToastService
    {
        public List<string> Messages { get; } = [];
        public int ClearCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task ShowMessage(string message, int seconds = 3)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3) => Task.CompletedTask;

        public Task Clear()
        {
            ClearCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public int OpenSongPublicationCalls { get; private set; }
        public int OpenMusicSectionCalls { get; private set; }
        public int OpenMusicTrackCalls { get; private set; }
        public int OpenLanguageModalCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task NavigateToHomeAsync(bool animated = true) => Task.CompletedTask;

        public Task NavigateToScheduleAsync() => Task.CompletedTask;

        public Task NavigateToScheduleAsync(int scheduleId, bool isEnabled) => Task.CompletedTask;

        public Task OpenSongPublicationSelectionModalAsync(object bindingContext)
        {
            OpenSongPublicationCalls++;
            return Task.CompletedTask;
        }

        public Task OpenMusicTrackSelectionModalAsync(object bindingContext)
        {
            OpenMusicTrackCalls++;
            return Task.CompletedTask;
        }

        public Task OpenBibleSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenMusicSectionSelectionModalAsync(object bindingContext)
        {
            OpenMusicSectionCalls++;
            return Task.CompletedTask;
        }

        public Task OpenBiblePublicationTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenNumberOfTracksModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenLanguageModalAsync(object bindingContext)
        {
            OpenLanguageModalCalls++;
            return Task.CompletedTask;
        }

        public Task OpenCategoryModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenPlaybackModalAsync(bool animated = false) => Task.CompletedTask;

        public Task OpenBatteryOptimizationModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenNotificationPermissionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task PopModalAsync() => Task.CompletedTask;

        public Task PopAsync() => Task.CompletedTask;

        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;

        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Bible.Alarm.Views.Home? GetCurrentHomePage() => null;

        public Microsoft.Maui.Controls.Page? GetCurrentPage() => null;

        public bool IsPlaybackModalOnScreen() => false;

        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private sealed class ConfigurableScheduleSelectionService : IScheduleSelectionService
    {
        public AlarmMusic? MusicToLoad { get; set; }

        public void Dispose()
        {
        }

        public AlarmMusic? LoadMusicForSelection(LoadMusicForSelectionArgs args) => MusicToLoad;

        public BiblePublicationSchedule? LoadBiblePublicationForSelection(LoadBiblePublicationForSelectionArgs args) => null;
    }

    private sealed class SelectableLanguageMediaService : IdleCatalogMediaService
    {
        public Dictionary<string, Language> Languages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public override Task<Dictionary<string, Language>> GetBiblePublicationLanguages(
            string? categoryName = null,
            bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(Languages);

        public override Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(
            string languageCode,
            string versionCode,
            string? sectionCode) =>
            Task.FromResult(Tracks);

        public SortedDictionary<string, BiblePublicationTrack> Tracks { get; set; } = [];
    }

    private sealed class NopDispatcher : Fluxor.IDispatcher
    {
#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action)
        {
        }
    }

    private static ScheduleStateItem BaseSchedule(
        string? publicationCode = "osg",
        string? languageCode = "E",
        string? sectionCode = null,
        int? publicationModalItemCount = null,
        int? sectionModalItemCount = null) =>
        new()
        {
            Id = 3,
            Name = "Morning",
            IsEnabled = true,
            Hour = 6,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
            MusicPublicationCode = publicationCode,
            MusicLanguageCode = languageCode,
            MusicSectionCode = sectionCode,
            MusicTrackCode = "1",
            MusicRepeat = false,
            MusicPublicationModalItemCount = publicationModalItemCount,
            MusicSectionModalItemCount = sectionModalItemCount,
        };

    private static AlarmMusic SampleMusic() =>
        new()
        {
            PublicationCode = "osg",
            LanguageCode = "E",
            TrackCode = "1",
            Repeat = false,
        };

    private static MusicPublicationSelectionViewModel CreateMusicPublicationVm(
        MutableApplicationState state,
        INavigationService navigation,
        IMediaService media,
        IServiceScopeFactory scopeFactory)
    {
        var nested = new ServiceCollection();
        nested.AddSingleton(media);
        nested.AddSingleton<IServiceScopeFactory>(scopeFactory);
        nested.AddSingleton<ILanguageNameService>(new IdleLanguageNameService());
        var provider = nested.BuildServiceProvider();
        return new MusicPublicationSelectionViewModel(
            new MusicPublicationSelectionViewModelDeps(
                media,
                scopeFactory,
                state,
                new NopDispatcher(),
                navigation,
                provider));
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

    private static MusicSectionSelectionViewModel CreateMusicSectionVm(
        MutableApplicationState state,
        INavigationService navigation,
        IMediaService media) =>
        new(
            TestLogging.CreateLogger(),
            media,
            state,
            new NopDispatcher(),
            navigation);

    private static MusicTrackSelectionViewModel CreateMusicTrackVm(
        MutableApplicationState state,
        INavigationService navigation,
        IMediaService media) =>
        new(
            TestLogging.CreateLogger(),
            media,
            navigation,
            state,
            new NopDispatcher());

    private static (MusicCommandInitializer Sut, RecordingDispatcher Dispatcher, RecordingNavigationService Navigation, ConfigurableScheduleSelectionService Selection)
        CreateSut(
            MutableApplicationState state,
            IToastService toast,
            IMediaService? media = null,
            IServiceScopeFactory? scopeFactory = null,
            DbContextOptions<MediaDbContext>? mediaDbOptions = null,
            AlarmMusic? musicToLoad = null)
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new RecordingNavigationService();
        var selection = new ConfigurableScheduleSelectionService { MusicToLoad = musicToLoad };
        var mediaService = media ?? new IdleCatalogMediaService();
        // MS.DI special-cases IServiceScopeFactory to the root ServiceProvider, so MediaTestScopeFactory
        // registered via AddSingleton is ignored. Register MediaDbContext on the collection instead.
        var factory = scopeFactory
            ?? (mediaDbOptions != null
                ? new MediaTestScopeFactory(mediaDbOptions)
                : new ViewModelTestDoubles.StubScopeFactory());

        var services = new ServiceCollection();
        services.AddSingleton<IMediaService>(mediaService);
        if (mediaDbOptions != null)
        {
            services.AddSingleton(mediaDbOptions);
            services.AddScoped(sp => new MediaDbContext(sp.GetRequiredService<DbContextOptions<MediaDbContext>>()));
        }

        services.AddSingleton<IServiceScopeFactory>(factory);
        services.AddSingleton(CreateMusicPublicationVm(state, navigation, mediaService, factory));
        services.AddSingleton(CreateMusicSectionVm(state, navigation, mediaService));
        services.AddSingleton(CreateMusicTrackVm(state, navigation, mediaService));
        var provider = services.BuildServiceProvider();

        var deps = new MusicSelectionContainerViewModelDeps(
            TestLogging.CreateLogger(),
            navigation,
            selection,
            mediaService,
            state,
            dispatcher,
            ViewModelTestDoubles.CreateMapper(),
            provider,
            toast);

        return (new MusicCommandInitializer(deps), dispatcher, navigation, selection);
    }

    private static async Task ExecuteAsync(System.Windows.Input.ICommand command)
    {
        if (command is IAsyncRelayCommand asyncRelay)
        {
            await asyncRelay.ExecuteAsync(null);
            return;
        }

        command.Execute(null);
    }

    private static void WaitForDispatch(RecordingDispatcher dispatcher, int expectedCount = 1)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (dispatcher.Dispatched.Count < expectedCount && DateTime.UtcNow < deadline)
        {
            Thread.SpinWait(50_000);
        }
    }

    [Fact]
    public void Ctor_accepts_deps_record()
    {
        var deps = new MusicSelectionContainerViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!);
        var sut = new MusicCommandInitializer(deps);
        Assert.NotNull(sut);
    }

    [Fact]
    public void CreateToggleRepeatCommand_no_op_when_current_schedule_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var toast = new RecordingToastService();
        var (sut, _, _, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = null }),
            toast);

        var command = sut.CreateToggleRepeatCommand();
        Assert.IsType<RelayCommand>(command);
        command.Execute(null);

        Assert.Empty(toast.Messages);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void CreateToggleRepeatCommand_toggles_repeat_and_dispatches_draft_update()
    {
        var schedule = BaseSchedule();
        schedule.MusicRepeat = false;
        var toast = new RecordingToastService();
        var (sut, dispatcher, _, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = schedule }),
            toast);

        sut.CreateToggleRepeatCommand().Execute(null);

        Assert.Equal(AppConstants.ToastMessages.RepeatEnabled, Assert.Single(toast.Messages));
        WaitForDispatch(dispatcher);
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(Assert.Single(dispatcher.Dispatched));
        Assert.True(update.Schedule.MusicRepeat);
    }

    [Fact]
    public void CreateToggleRepeatCommand_clears_toast_when_repeat_disabled()
    {
        var schedule = BaseSchedule();
        schedule.MusicRepeat = true;
        var toast = new RecordingToastService();
        var (sut, dispatcher, _, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = schedule }),
            toast);

        sut.CreateToggleRepeatCommand().Execute(null);

        Assert.Equal(1, toast.ClearCalls);
        Assert.Empty(toast.Messages);
        WaitForDispatch(dispatcher);
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(Assert.Single(dispatcher.Dispatched));
        Assert.False(update.Schedule.MusicRepeat);
    }

    [Fact]
    public async Task CreateSelectMusicCommand_opens_publication_modal_and_sets_loaded_music()
    {
        var music = SampleMusic();
        AlarmMusic? setMusic = null;
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule() }),
            toast,
            musicToLoad: music);

        await ExecuteAsync(sut.CreateSelectMusicCommand(() => null, m => setMusic = m, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Same(music, setMusic);
        Assert.Equal(1, navigation.OpenSongPublicationCalls);
        Assert.IsType<MusicSelectionAction>(Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task CreateSelectMusicCommand_skips_dispatch_when_loaded_music_is_null()
    {
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule() }),
            toast,
            musicToLoad: null);

        await ExecuteAsync(sut.CreateSelectMusicCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: true, musicUpdated: false));

        Assert.Equal(1, navigation.OpenSongPublicationCalls);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task CreateSelectSongPublicationCommand_returns_early_when_not_selectable()
    {
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule() }),
            toast);

        await ExecuteAsync(sut.CreateSelectSongPublicationCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Equal(0, navigation.OpenSongPublicationCalls);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task CreateSelectSongPublicationCommand_opens_modal_when_multiple_publications_exist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

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

            foreach (var code in new[] { "pub-a", "pub-b" })
            {
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = code,
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = true,
                });
            }

            await seed.SaveChangesAsync();
        }

        var music = SampleMusic();
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule(languageCode: "E") }),
            toast,
            mediaDbOptions: options,
            musicToLoad: music);

        await ExecuteAsync(sut.CreateSelectSongPublicationCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Equal(1, navigation.OpenSongPublicationCalls);
        Assert.IsType<MusicPublicationSelectionAction>(Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task CreateSelectMusicTypeCommand_redirects_to_publication_selection_gate()
    {
        var toast = new RecordingToastService();
        var (sut, _, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule() }),
            toast);

        await ExecuteAsync(sut.CreateSelectMusicTypeCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Equal(0, navigation.OpenSongPublicationCalls);
    }

    [Fact]
    public async Task CreateSelectMusicSectionCommand_returns_early_when_publication_has_no_sections()
    {
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule(publicationCode: "osg") }),
            toast);

        await ExecuteAsync(sut.CreateSelectMusicSectionCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Equal(0, navigation.OpenMusicSectionCalls);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task CreateSelectMusicSectionCommand_opens_modal_when_multiple_sections_exist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

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
                CategoryId = musicCat.Id,
                Language = lang,
                LanguageId = lang.Id,
                IsMusic = true,
            };
            seed.PublicationLanguages.Add(pl);
            await seed.SaveChangesAsync();

            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                SectionCode = "iam-1",
                Language = lang,
                PublicationLanguage = pl,
            });
            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                SectionCode = "iam-2",
                Language = lang,
                PublicationLanguage = pl,
            });
            await seed.SaveChangesAsync();
        }

        var music = SampleMusic();
        music.PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam;
        music.LanguageCode = null;
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState
            {
                CurrentSchedule = BaseSchedule(
                    publicationCode: AppConstants.Media.MelodyMusicPublicationCodeIam,
                    languageCode: AppConstants.Media.DefaultLanguageCode),
            }),
            toast,
            mediaDbOptions: options,
            musicToLoad: music);

        await ExecuteAsync(sut.CreateSelectMusicSectionCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Equal(1, navigation.OpenMusicSectionCalls);
        Assert.IsType<MusicPublicationSelectionAction>(Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task CreateSelectTrackCommand_returns_early_when_fewer_than_two_tracks()
    {
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule() }),
            toast,
            media: new SelectableLanguageMediaService());

        await ExecuteAsync(sut.CreateSelectTrackCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Equal(0, navigation.OpenMusicTrackCalls);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task CreateSelectTrackCommand_opens_modal_when_multiple_tracks_exist()
    {
        var media = new SelectableLanguageMediaService
        {
            Tracks = new SortedDictionary<string, BiblePublicationTrack>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = new BiblePublicationTrack { TrackCode = "1", Title = "One" },
                ["2"] = new BiblePublicationTrack { TrackCode = "2", Title = "Two" },
            },
        };
        var music = SampleMusic();
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule() }),
            toast,
            media: media,
            musicToLoad: music);

        await ExecuteAsync(sut.CreateSelectTrackCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Equal(1, navigation.OpenMusicTrackCalls);
        Assert.IsType<MusicTrackSelectionAction>(Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task CreateSelectMusicLanguageCommand_returns_early_when_single_language()
    {
        var media = new SelectableLanguageMediaService
        {
            Languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
            },
        };
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule() }),
            toast,
            media: media);

        await ExecuteAsync(sut.CreateSelectMusicLanguageCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Equal(0, navigation.OpenLanguageModalCalls);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task CreateSelectMusicLanguageCommand_opens_language_modal_when_multiple_languages_exist()
    {
        var media = new SelectableLanguageMediaService
        {
            Languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
                ["S"] = new Language { LanguageCode = "S", Direction = AppConstants.Media.TextDirectionLeftToRight },
            },
        };
        var music = SampleMusic();
        var toast = new RecordingToastService();
        var (sut, dispatcher, navigation, _) = CreateSut(
            new MutableApplicationState(new ApplicationState { CurrentSchedule = BaseSchedule() }),
            toast,
            media: media,
            musicToLoad: music);

        await ExecuteAsync(sut.CreateSelectMusicLanguageCommand(() => null, _ => { }, scheduleId: 3, isNewSchedule: false, musicUpdated: false));

        Assert.Equal(1, navigation.OpenLanguageModalCalls);
        Assert.IsType<MusicSelectionAction>(Assert.Single(dispatcher.Dispatched));
    }
}
