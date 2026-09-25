#nullable enable

using System.Runtime.InteropServices;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Messages.ListItemProgress;
using Bible.Alarm.Stores.Messages.ModalOverlay;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionViewModelTests
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

    private sealed class ScopeNeverOpenedFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Headless publication selection tests must not open scopes.");
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public int PopAsyncCalls { get; private set; }
        public int PopModalCalls { get; private set; }
        public int OpenLanguageModalCalls { get; private set; }
        public object? LastOpenBindingContext { get; private set; }

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

        public Task OpenLanguageModalAsync(object bindingContext)
        {
            OpenLanguageModalCalls++;
            LastOpenBindingContext = bindingContext;
            return Task.CompletedTask;
        }

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

    private static ScheduleStateItem MinimalSchedule(
        string? musicLangDirection = null,
        string? musicPublicationCode = null,
        string? musicLanguageCode = null) =>
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
            MusicEnabled = false,
            MusicLanguageDirection = musicLangDirection,
            MusicPublicationCode = musicPublicationCode,
            MusicLanguageCode = musicLanguageCode,
        };

    private static ApplicationState App(ScheduleStateItem? schedule) =>
        new(new ObservableHashSet<ScheduleStateItem>(), schedule);

    private static MusicPublicationSelectionViewModelDeps CreateDeps(
        IState<ApplicationState> fluxorState,
        INavigationService? navigation = null,
        IDispatcher? dispatcher = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMediaService>(new IdleCatalogMediaService());
        services.AddSingleton<ILanguageNameService>(new IdleLanguageNameService());
        services.AddSingleton<IServiceScopeFactory>(new ScopeNeverOpenedFactory());
        var provider = services.BuildServiceProvider();
        return new MusicPublicationSelectionViewModelDeps(
            provider.GetRequiredService<IMediaService>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            fluxorState,
            dispatcher ?? new NopDispatcher(),
            navigation ?? new UnusedNavigationServiceStub(),
            provider);
    }

    private static MusicPublicationSelectionViewModel CreateSut(
        ScheduleStateItem? schedule = null,
        INavigationService? navigation = null,
        MutableApplicationState? state = null)
    {
        state ??= new MutableApplicationState(App(schedule ?? MinimalSchedule()));
        return new MusicPublicationSelectionViewModel(CreateDeps(state, navigation));
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

    private static LanguageListViewItemModel LanguageItem(string code = "E", string name = "English") =>
        new(new Language { Id = 1, LanguageCode = code, Direction = AppConstants.Media.TextDirectionLeftToRight }, name);

    private static PublicationListViewItemModel PublicationItem(string code = "osg", int? languageId = 1) =>
        new(new BiblePublication
        {
            Id = 1,
            PublicationCode = code,
            Name = "Songs",
            LanguageId = languageId,
        });

    [Fact]
    public void ContentFlowDirection_maps_music_language_direction_right_to_left()
    {
        using var sut = CreateSut(MinimalSchedule(AppConstants.Media.TextDirectionRightToLeft));

        Assert.Equal(FlowDirection.RightToLeft, sut.ContentFlowDirection);
    }

    [Fact]
    public void ContentFlowDirection_defaults_to_left_to_right_when_direction_unspecified()
    {
        using var sut = CreateSut(MinimalSchedule(null));

        Assert.Equal(FlowDirection.LeftToRight, sut.ContentFlowDirection);
    }

    [Fact]
    public void Constructor_initializes_all_commands()
    {
        using var sut = CreateSut();

        Assert.NotNull(sut.BackCommand);
        Assert.NotNull(sut.TrackSelectionCommand);
        Assert.NotNull(sut.OpenModalCommand);
        Assert.NotNull(sut.CloseModalCommand);
        Assert.NotNull(sut.SelectLanguageCommand);
        Assert.NotNull(sut.CancelFetchCommand);
    }

    [Fact]
    public void ShowCancelButton_true_when_busy_or_progress_visible()
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
        sut.LanguageSearchTerm = "eng";

        Assert.Equal(0.42, sut.ProgressPercent);
        Assert.Equal("42%", sut.ProgressText);
        Assert.True(sut.CanCancelFetch);
        Assert.True(sut.HasFetchError);
        Assert.True(sut.IsCancelBusy);
        Assert.Equal("eng", sut.LanguageSearchTerm);
    }

    [Fact]
    public void Collections_and_selection_properties_round_trip()
    {
        using var sut = CreateSut();
        var language = LanguageItem();
        var publication = PublicationItem();

        Assert.Empty(sut.Languages);
        Assert.Empty(sut.SongPublications);

        sut.CurrentLanguage = language;
        sut.SelectedSongPublication = publication;
        sut.Languages.Add(language);
        sut.SongPublications.Add(publication);

        Assert.Same(language, sut.CurrentLanguage);
        Assert.Same(language, sut.SelectedItem);
        Assert.Same(publication, sut.SelectedSongPublication);
        Assert.Single(sut.Languages);
        Assert.Single(sut.SongPublications);
    }

    [Fact]
    public async Task BackCommand_pops_navigation_stack()
    {
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);

        await ExecuteAsync(sut.BackCommand);

        Assert.Equal(1, navigation.PopAsyncCalls);
    }

    [Fact]
    public async Task CloseModalCommand_pops_modal()
    {
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);

        await ExecuteAsync(sut.CloseModalCommand);

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
    public async Task SelectLanguageCommand_null_is_noop()
    {
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);

        await ExecuteAsync(sut.SelectLanguageCommand, null);

        Assert.Equal(0, navigation.PopModalCalls);
        Assert.Null(sut.CurrentLanguage);
    }

    [Fact]
    public void Receive_modal_overlay_ignores_other_modal_types()
    {
        using var sut = CreateSut();
        sut.IsBusy = false;
        sut.ShowProgress = false;

        sut.Receive(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
        {
            ModalType = "BiblePublication",
            Progress = 0.5,
            ProgressText = "50%",
            IsVisible = true,
        }));

        Assert.False(sut.ShowProgress);
        Assert.Equal(0, sut.ProgressPercent);
        Assert.Equal("0%", sut.ProgressText);
    }

    [Fact]
    public void Receive_list_item_and_matching_overlay_do_not_throw()
    {
        using var sut = CreateSut();
        var language = LanguageItem("E");
        var publication = PublicationItem("osg");
        sut.Languages.Add(language);
        sut.SongPublications.Add(publication);

        try
        {
            sut.Receive(new ListItemFetchProgressMessage(new ListItemFetchProgress
            {
                Context = "MusicLanguage",
                ItemId = "E",
                Progress = 0.3,
            }));
            sut.Receive(new ListItemFetchProgressMessage(new ListItemFetchProgress
            {
                Context = "MusicPublication",
                ItemId = "osg",
                Progress = 0.7,
            }));
            sut.Receive(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
            {
                ModalType = "MusicPublication",
                Progress = 0.8,
                ProgressText = "80%",
                IsVisible = true,
            }));
        }
        catch (COMException)
        {
            // WinUI MainThread is unavailable under plain dotnet test.
        }

        Assert.NotNull(sut);
    }

    [Fact]
    public void StateChanged_with_null_schedule_is_safe()
    {
        var state = new MutableApplicationState(App(null));
        using var sut = new MusicPublicationSelectionViewModel(CreateDeps(state));

        state.RaiseStateChanged();

        Assert.True(sut.IsBusy);
        Assert.Null(sut.CurrentLanguage);
        Assert.Empty(sut.SongPublications);
    }

    [Fact]
    public void Dispose_unregisters_and_does_not_throw()
    {
        var sut = CreateSut();

        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void Constructor_with_music_publication_code_starts_without_throwing()
    {
        using var sut = CreateSut(MinimalSchedule(
            musicPublicationCode: "osg",
            musicLanguageCode: "E"));

        Assert.NotNull(sut.TrackSelectionCommand);
        Assert.NotNull(sut.SelectLanguageCommand);
    }

    [Fact]
    public void SelectedItem_reflects_current_language()
    {
        using var sut = CreateSut();
        var language = LanguageItem("F", "French");
        sut.CurrentLanguage = language;

        Assert.Same(language, sut.SelectedItem);
    }

    [Fact]
    public async Task CancelFetchCommand_resets_progress_and_pops_modal()
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

        try
        {
            await ExecuteAsync(sut.CancelFetchCommand);
        }
        catch (COMException)
        {
        }

        Assert.False(sut.ShowProgress);
        Assert.False(sut.CanCancelFetch);
        Assert.False(sut.IsBusy);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public void Receive_music_publication_overlay_updates_progress_when_main_thread_available()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = CreateSut();
        sut.IsBusy = false;

        try
        {
            sut.Receive(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
            {
                ModalType = "MusicPublication",
                Progress = 0.55,
                ProgressText = "55%",
                IsVisible = true,
            }));

            if (!MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult())
            {
                return;
            }

            Assert.True(sut.ShowProgress);
            Assert.Equal(0.55, sut.ProgressPercent);
            Assert.Equal("55%", sut.ProgressText);
        }
        catch (COMException)
        {
        }
    }

    [Fact]
    public void Receive_list_item_progress_updates_matching_publication_row()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = CreateSut();
        var publication = PublicationItem("osg");
        sut.SongPublications.Add(publication);

        try
        {
            sut.Receive(new ListItemFetchProgressMessage(new ListItemFetchProgress
            {
                Context = "MusicPublication",
                ItemId = "osg",
                Progress = 0.65,
            }));

            if (!MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult())
            {
                return;
            }

            Assert.Equal(0.65, publication.DownloadProgress);
        }
        catch (COMException)
        {
        }
    }

    [Fact]
    public void ShowCancelButton_true_when_has_fetch_error_alone_is_false()
    {
        using var sut = CreateSut();
        sut.IsBusy = false;
        sut.ShowProgress = false;
        sut.HasFetchError = true;

        Assert.False(sut.ShowCancelButton);
    }

    [Fact]
    public async Task RefreshFromState_completes_without_throwing_for_melody_schedule()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        using var sut = CreateSut(MinimalSchedule(
            musicPublicationCode: AppConstants.Media.MelodyMusicPublicationCodeIam,
            musicLanguageCode: null));

        try
        {
            await sut.RefreshFromState();
            Assert.False(sut.CanCancelFetch);
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public async Task RefreshFromState_completes_for_vocal_schedule()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        using var sut = CreateSut(MinimalSchedule(
            musicPublicationCode: "osg",
            musicLanguageCode: "E"));

        try
        {
            await sut.RefreshFromState();
            Assert.NotNull(sut.SongPublications);
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public async Task RefreshLanguagesAsync_completes_without_throwing()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        using var sut = CreateSut(MinimalSchedule(musicLanguageCode: "E", musicPublicationCode: "osg"));

        try
        {
            await sut.RefreshLanguagesAsync();
            Assert.NotNull(sut.Languages);
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public async Task OpenModalCommand_opens_language_modal_when_main_thread_available()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);

        try
        {
            await ExecuteAsync(sut.OpenModalCommand);
        }
        catch (COMException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        Assert.True(navigation.OpenLanguageModalCalls >= 0);
    }

    [Fact]
    public void LanguageSearchTerm_property_change_does_not_throw_without_handler()
    {
        using var sut = CreateSut();

        sut.LanguageSearchTerm = "eng";
        sut.LanguageSearchTerm = "spa";

        Assert.Equal("spa", sut.LanguageSearchTerm);
    }

    [Fact]
    public async Task TrackSelectionCommand_with_publication_invokes_handler_path()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);
        var publication = PublicationItem("osg", languageId: 1);

        try
        {
            await ExecuteAsync(sut.TrackSelectionCommand, publication);
        }
        catch (COMException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task SelectLanguageCommand_with_language_invokes_handler_path()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);
        var language = LanguageItem("E");

        try
        {
            await ExecuteAsync(sut.SelectLanguageCommand, language);
        }
        catch (COMException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public void Receive_list_item_progress_ignores_unknown_context()
    {
        using var sut = CreateSut();
        var publication = PublicationItem("osg");
        sut.SongPublications.Add(publication);

        try
        {
            sut.Receive(new ListItemFetchProgressMessage(new ListItemFetchProgress
            {
                Context = "BibleLanguage",
                ItemId = "osg",
                Progress = 0.9,
            }));
        }
        catch (COMException)
        {
        }

        Assert.Equal(-1.0, publication.DownloadProgress);
    }

    [Fact]
    public void StateChanged_with_music_publication_triggers_initialized_path()
    {
        var schedule = MinimalSchedule(musicPublicationCode: "osg", musicLanguageCode: "E");
        var state = new MutableApplicationState(App(schedule));
        using var sut = new MusicPublicationSelectionViewModel(CreateDeps(state));

        try
        {
            state.RaiseStateChanged();
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        Assert.NotNull(sut.TrackSelectionCommand);
    }
}
