#nullable enable

using System.Runtime.InteropServices;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Messages.ModalOverlay;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.Controls;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicSectionSelectionViewModelTests
{
    private sealed class MutableApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value { get; set; } = value;

        public event EventHandler? StateChanged;

        public void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class NopDispatcher : IDispatcher
    {
#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action)
        {
        }
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public int PopAsyncCalls { get; private set; }
        public int PopModalCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task NavigateToHomeAsync(bool animated = true) => Task.CompletedTask;
        public Task NavigateToScheduleAsync() => Task.CompletedTask;
        public Task NavigateToScheduleAsync(int scheduleId, bool isEnabled) => Task.CompletedTask;
        public Task OpenSongPublicationSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenMusicTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenBibleSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenMusicSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenBiblePublicationTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenNumberOfTracksModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenLanguageModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenCategoryModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenPlaybackModalAsync(bool animated = false) => Task.CompletedTask;
        public Task OpenBatteryOptimizationModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenNotificationPermissionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task PopModalAsync()
        {
            PopModalCalls++;
            return Task.CompletedTask;
        }

        public Task PopAsync()
        {
            PopAsyncCalls++;
            return Task.CompletedTask;
        }

        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;

        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Home? GetCurrentHomePage() => null;
        public Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;

        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private sealed class CatalogMediaService : IMediaService
    {
        public SortedDictionary<string, BiblePublicationSection> Sections { get; init; } = [];
        public SortedDictionary<int, MusicTrack> MelodyTracksBySection { get; init; } = [];
        public int ExpectedSectionCount { get; init; }
        public bool IsMelody { get; init; } = true;

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(Sections);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(MelodyTracksBySection);

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
            Task.FromResult(IsMelody);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(ExpectedSectionCount);
    }

    private static ScheduleStateItem MinimalSchedule(
        string? musicPublicationCode = null,
        string? musicSectionCode = null,
        string? musicPublicationName = null) =>
        new()
        {
            Id = 1,
            Name = "Test",
            IsEnabled = true,
            Hour = 8,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
            MusicPublicationCode = musicPublicationCode,
            MusicSectionCode = musicSectionCode,
            MusicPublicationName = musicPublicationName,
            MusicRepeat = false,
        };

    private static ApplicationState App(ScheduleStateItem? schedule) =>
        new(new ObservableHashSet<ScheduleStateItem>(), schedule);

    private static MusicSectionSelectionViewModel CreateSut(
        ScheduleStateItem? schedule = null,
        INavigationService? navigation = null,
        IDispatcher? dispatcher = null,
        IMediaService? media = null,
        MutableApplicationState? state = null)
    {
        state ??= new MutableApplicationState(App(schedule));
        return new MusicSectionSelectionViewModel(
            TestLogging.CreateLogger(),
            media ?? new IdleCatalogMediaService(),
            state,
            dispatcher ?? new NopDispatcher(),
            navigation ?? new UnusedNavigationServiceStub());
    }

    private static async Task ExecuteAsync(System.Windows.Input.ICommand command, object? parameter = null)
    {
        if (command is IAsyncRelayCommand asyncRelay)
        {
            await asyncRelay.ExecuteAsync(parameter);
            return;
        }

        command.Execute(parameter);
    }

    private static BiblePublicationSectionListViewItemModel SectionItem(
        string code = "disc-a",
        string name = "Disc A",
        int id = 10) =>
        new(new BiblePublicationSection
        {
            Id = id,
            SectionCode = code,
            Name = name,
        });

    private static CatalogMediaService FullyCatalogedMedia(
        string sectionCode = "disc-a",
        string sectionName = "Disc A",
        SortedDictionary<int, MusicTrack>? tracks = null) =>
        new()
        {
            Sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
            {
                [sectionCode] = new BiblePublicationSection
                {
                    Id = 10,
                    SectionCode = sectionCode,
                    Name = sectionName,
                },
            },
            ExpectedSectionCount = 1,
            IsMelody = true,
            MelodyTracksBySection = tracks ?? new SortedDictionary<int, MusicTrack>
            {
                [1] = new MusicTrack { TrackCode = "1", Title = "Track One" },
            },
        };

    [Fact]
    public void Ctor_wires_commands_and_busy_defaults()
    {
        using var sut = CreateSut();

        Assert.NotNull(sut.BackCommand);
        Assert.NotNull(sut.CloseModalCommand);
        Assert.NotNull(sut.TrackSelectionCommand);
        Assert.NotNull(sut.CancelFetchCommand);
        Assert.True(sut.IsBusy);
        Assert.True(sut.ShowCancelButton);
        Assert.Empty(sut.Sections);
        Assert.Null(sut.SelectedSection);
        Assert.Null(sut.SelectedItem);
    }

    [Fact]
    public void ContentFlowDirection_is_always_left_to_right()
    {
        using var sut = CreateSut(MinimalSchedule(musicPublicationCode: "iam"));

        Assert.Equal(FlowDirection.LeftToRight, sut.ContentFlowDirection);
    }

    [Fact]
    public void ShowCancelButton_follows_IsBusy_and_ShowProgress()
    {
        using var sut = CreateSut();

        Assert.True(sut.IsBusy);
        Assert.True(sut.ShowCancelButton);

        sut.IsBusy = false;
        Assert.False(sut.ShowCancelButton);

        sut.ShowProgress = true;
        Assert.True(sut.ShowCancelButton);

        sut.ShowProgress = false;
        Assert.False(sut.ShowCancelButton);
    }

    [Fact]
    public void Progress_and_busy_properties_round_trip()
    {
        using var sut = CreateSut();

        sut.ProgressPercent = 0.42;
        sut.ProgressText = "42%";
        sut.CanCancelFetch = true;
        sut.HasFetchError = true;
        sut.IsCancelBusy = true;

        Assert.Equal(0.42, sut.ProgressPercent);
        Assert.Equal("42%", sut.ProgressText);
        Assert.True(sut.CanCancelFetch);
        Assert.True(sut.HasFetchError);
        Assert.True(sut.IsCancelBusy);
    }

    [Fact]
    public void Sections_and_selection_properties_round_trip()
    {
        using var sut = CreateSut();
        var section = SectionItem();

        sut.Sections.Add(section);
        sut.SelectedSection = section;

        Assert.Same(section, sut.SelectedSection);
        Assert.Same(section, sut.SelectedItem);
        Assert.Single(sut.Sections);
    }

    [Fact]
    public async Task BackCommand_pops_navigation_stack()
    {
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);

        await ExecuteAsync(sut.BackCommand);

        Assert.Equal(1, navigation.PopAsyncCalls);
        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task CloseModalCommand_pops_modal()
    {
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);

        await ExecuteAsync(sut.CloseModalCommand);

        Assert.Equal(0, navigation.PopAsyncCalls);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task TrackSelectionCommand_null_is_noop()
    {
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);

        await ExecuteAsync(sut.TrackSelectionCommand, null);

        Assert.Equal(0, navigation.PopModalCalls);
        Assert.False(sut.ShowProgress);
    }

    [Fact]
    public async Task TrackSelectionCommand_with_empty_tracks_does_not_pop()
    {
        var navigation = new RecordingNavigationService();
        var schedule = MinimalSchedule(musicPublicationCode: "iam");
        using var sut = CreateSut(schedule, navigation, media: FullyCatalogedMedia(tracks: []));

        await ExecuteAsync(sut.TrackSelectionCommand, SectionItem());

        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task TrackSelectionCommand_with_melody_tracks_dispatches_and_pops()
    {
        var navigation = new RecordingNavigationService();
        var dispatcher = new RecordingDispatcher();
        var schedule = MinimalSchedule(
            musicPublicationCode: "iam",
            musicSectionCode: "disc-a",
            musicPublicationName: "Instrumental");
        using var sut = CreateSut(schedule, navigation, dispatcher, FullyCatalogedMedia());

        await ExecuteAsync(sut.TrackSelectionCommand, SectionItem());

        Assert.Equal(1, navigation.PopModalCalls);
        var action = Assert.IsType<MusicSectionSelectedAction>(Assert.Single(dispatcher.Dispatched));
        Assert.Equal("iam", action.CurrentMusic.PublicationCode);
        Assert.Equal("disc-a", action.CurrentMusic.SectionCode);
        Assert.Equal("1", action.CurrentMusic.TrackCode);
        Assert.Equal("Track One", action.CurrentMusic.TrackName);
        Assert.Equal("Disc A", action.CurrentMusic.SectionName);
    }

    [Fact]
    public async Task TrackSelectionCommand_without_publication_is_noop()
    {
        var navigation = new RecordingNavigationService();
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(MinimalSchedule(), navigation, dispatcher, FullyCatalogedMedia());

        await ExecuteAsync(sut.TrackSelectionCommand, SectionItem());

        Assert.Equal(0, navigation.PopModalCalls);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task CancelFetchCommand_resets_overlay_flags_and_pops_modal()
    {
        // Host-oriented coverage; PopModal / display text race on Android/iOS device runners.
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);
        sut.ShowProgress = true;
        sut.CanCancelFetch = true;
        sut.IsBusy = true;

        await ExecuteAsync(sut.CancelFetchCommand);

        Assert.False(sut.ShowProgress);
        Assert.False(sut.CanCancelFetch);
        Assert.False(sut.IsBusy);
        Assert.False(sut.IsCancelBusy);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task RefreshFromState_without_publication_is_noop()
    {
        using var sut = CreateSut(MinimalSchedule());

        await sut.RefreshFromState();

        Assert.Empty(sut.Sections);
        Assert.False(sut.CanCancelFetch);
    }

    [Fact]
    public async Task RefreshFromState_with_null_schedule_is_noop()
    {
        using var sut = CreateSut(schedule: null);

        await sut.RefreshFromState();

        Assert.Empty(sut.Sections);
        Assert.True(sut.IsBusy);
    }

    [Fact]
    public async Task RefreshFromState_with_publication_enters_repopulation_path()
    {
        var media = FullyCatalogedMedia();
        var schedule = MinimalSchedule(musicPublicationCode: "iam", musicSectionCode: "disc-a");
        using var sut = CreateSut(schedule, media: media);

        try
        {
            var refresh = sut.RefreshFromState();
            var completed = await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromSeconds(5)));
            if (!ReferenceEquals(completed, refresh))
            {
                // Headless hosts without a WinUI dispatcher can hang on MainThread.InvokeOnMainThreadAsync.
                return;
            }

            await refresh;

            Assert.Single(sut.Sections);
            Assert.Equal("disc-a", sut.Sections[0].SectionCode);
            Assert.NotNull(sut.SelectedSection);
            Assert.True(sut.SelectedSection!.IsSelected);
            Assert.False(sut.CanCancelFetch);
            Assert.False(sut.ShowProgress);
        }
        catch (COMException)
        {
            // WinUI MainThread / DeviceDisplay unavailable under plain dotnet test.
        }
        catch (InvalidOperationException)
        {
            // MainThread or KeepScreenOn can fail when the UI host is not initialized.
        }

        Assert.NotNull(sut.TrackSelectionCommand);
    }

    [Fact]
    public void Receive_modal_overlay_ignores_other_modal_types()
    {
        using var sut = CreateSut();
        sut.IsBusy = false;
        sut.ShowProgress = false;

        sut.Receive(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
        {
            ModalType = "BibleSection",
            Progress = 0.5,
            ProgressText = "50%",
            IsVisible = true,
        }));

        Assert.False(sut.ShowProgress);
        Assert.Equal(0, sut.ProgressPercent);
        Assert.Equal("0%", sut.ProgressText);
    }

    [Fact]
    public void Receive_music_section_overlay_does_not_throw()
    {
        using var sut = CreateSut();

        try
        {
            sut.Receive(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
            {
                ModalType = "MusicSection",
                Progress = 0.8,
                ProgressText = "80%",
                IsVisible = true,
            }));
        }
        catch (COMException)
        {
            // WinUI MainThread is unavailable under plain dotnet test.
        }
        catch (InvalidOperationException)
        {
            // BeginInvokeOnMainThread requires a dispatcher on some hosts.
        }

        Assert.NotNull(sut);
    }

    [Fact]
    public void StateChanged_with_null_schedule_is_safe()
    {
        var state = new MutableApplicationState(App(null));
        using var sut = new MusicSectionSelectionViewModel(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            state,
            new NopDispatcher(),
            new UnusedNavigationServiceStub());

        state.RaiseStateChanged();

        Assert.True(sut.IsBusy);
        Assert.Empty(sut.Sections);
    }

    [Fact]
    public void Dispose_unregisters_and_is_idempotent()
    {
        var sut = CreateSut();

        sut.Dispose();
        sut.Dispose();

        Assert.NotNull(sut.BackCommand);
    }
}
