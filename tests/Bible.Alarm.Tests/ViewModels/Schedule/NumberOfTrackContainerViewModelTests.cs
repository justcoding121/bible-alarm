#nullable enable

using AutoMapper;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests.ViewModels.Schedule;

public sealed class NumberOfTrackContainerViewModelTests
{
    private sealed class MutableApplicationState : IState<ApplicationState>
    {
        public MutableApplicationState(ApplicationState value) => Value = value;

        public ApplicationState Value { get; set; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public void NotifyChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public int OpenNumberOfTracksModalCalls { get; private set; }
        public int PopModalCalls { get; private set; }
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

        public Task OpenNumberOfTracksModalAsync(object bindingContext)
        {
            OpenNumberOfTracksModalCalls++;
            LastOpenBindingContext = bindingContext;
            return Task.CompletedTask;
        }

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

        public Task PopAsync() => Task.CompletedTask;
        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;
        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Home? GetCurrentHomePage() => null;
        public Microsoft.Maui.Controls.Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;
        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleStateItem Schedule(
        int id = 1,
        bool notificationEnabled = true,
        bool alwaysPlayFromStart = false,
        int numberOfTracksToPlay = 3,
        string? categoryName = "Bible") =>
        new()
        {
            Id = id,
            Name = "Test",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = notificationEnabled,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = numberOfTracksToPlay,
            AlwaysPlayFromStart = alwaysPlayFromStart,
            CurrentPlayItem = PlayType.Bible,
            BiblePublicationCategoryName = categoryName,
        };

    private static ApplicationState App(ScheduleStateItem? current) =>
        new(new ObservableHashSet<ScheduleStateItem>(), current);

    private static NumberOfTrackContainerViewModel CreateSut(
        MutableApplicationState state,
        RecordingDispatcher? dispatcher = null,
        INavigationService? navigation = null)
    {
        dispatcher ??= new RecordingDispatcher();
        return new NumberOfTrackContainerViewModel(
            TestLogging.CreateLogger(),
            navigation ?? new UnusedNavigationServiceStub(),
            new EmptyServiceProvider(),
            state,
            dispatcher,
            CreateMapper());
    }

    private static NumberOfTrackContainerViewModel CreateSutWithoutSchedule(RecordingDispatcher dispatcher) =>
        CreateSut(
            new MutableApplicationState(App(null)),
            dispatcher);

    private static async Task ExecuteAsync(System.Windows.Input.ICommand command)
    {
        if (command is IAsyncRelayCommand asyncRelay)
        {
            await asyncRelay.ExecuteAsync(null);
            return;
        }

        throw new InvalidOperationException("Expected IAsyncRelayCommand");
    }

    private static async Task ExecuteAsync<T>(System.Windows.Input.ICommand command, T parameter)
    {
        if (command is IAsyncRelayCommand<T> asyncRelay)
        {
            await asyncRelay.ExecuteAsync(parameter);
            return;
        }

        throw new InvalidOperationException($"Expected IAsyncRelayCommand<{typeof(T).Name}>");
    }

    [Fact]
    public void ToggleAlwaysPlayFromStart_flips_locally_without_dispatch_when_schedule_is_missing()
    {
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSutWithoutSchedule(dispatcher);

        Assert.IsType<RelayCommand>(sut.ToggleAlwaysPlayFromStartCommand).Execute(null);
        Assert.True(sut.AlwaysPlayFromStart);

        Assert.IsType<RelayCommand>(sut.ToggleAlwaysPlayFromStartCommand).Execute(null);
        Assert.False(sut.AlwaysPlayFromStart);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void Constructor_loads_flags_from_current_schedule()
    {
        var state = new MutableApplicationState(App(Schedule(
            notificationEnabled: false,
            alwaysPlayFromStart: true,
            numberOfTracksToPlay: 0)));
        using var sut = CreateSut(state);

        Assert.False(sut.NotificationEnabled);
        Assert.True(sut.AlwaysPlayFromStart);
        Assert.True(sut.PlayIndefinitely);
        Assert.False(sut.IsNumberOfTracksSelectionVisible);
    }

    [Fact]
    public void Constructor_initializes_all_commands()
    {
        using var sut = CreateSutWithoutSchedule(new RecordingDispatcher());

        Assert.NotNull(sut.OpenModalCommand);
        Assert.NotNull(sut.SelectNumberOfTracksCommand);
        Assert.NotNull(sut.ToggleAlwaysPlayFromStartCommand);
        Assert.NotNull(sut.TogglePlayIndefinitelyCommand);
        Assert.NotNull(sut.NotificationEnabledCommand);
        Assert.NotNull(sut.CloseModalCommand);
        Assert.NotNull(sut.OverlayCancelCommand);
    }

    [Fact]
    public void ToggleAlwaysPlayFromStartCommand_dispatches_update_when_schedule_present()
    {
        var state = new MutableApplicationState(App(Schedule(alwaysPlayFromStart: false)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        Assert.IsType<RelayCommand>(sut.ToggleAlwaysPlayFromStartCommand).Execute(null);

        Assert.True(sut.AlwaysPlayFromStart);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.True(update.Schedule.AlwaysPlayFromStart);
        Assert.False(update.ShouldSave);
    }

    [Fact]
    public void TogglePlayIndefinitelyCommand_on_dispatches_zero_tracks()
    {
        var state = new MutableApplicationState(App(Schedule(numberOfTracksToPlay: 3)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        Assert.IsType<RelayCommand>(sut.TogglePlayIndefinitelyCommand).Execute(null);

        Assert.True(sut.PlayIndefinitely);
        Assert.False(sut.IsNumberOfTracksSelectionVisible);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal(0, update.Schedule.NumberOfTracksToPlay);
    }

    [Fact]
    public async Task TogglePlayIndefinitelyCommand_off_dispatches_selected_or_default_tracks()
    {
        var state = new MutableApplicationState(App(Schedule(numberOfTracksToPlay: 0)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        await sut.PopulateNumberOfTracksListViewAsync(5);
        dispatcher.Dispatched.Clear();

        Assert.IsType<RelayCommand>(sut.TogglePlayIndefinitelyCommand).Execute(null);

        Assert.False(sut.PlayIndefinitely);
        Assert.True(sut.IsNumberOfTracksSelectionVisible);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal(5, update.Schedule.NumberOfTracksToPlay);
    }

    [Fact]
    public void NotificationEnabledCommand_dispatches_update_when_schedule_present()
    {
        var state = new MutableApplicationState(App(Schedule(notificationEnabled: true)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        Assert.IsType<RelayCommand>(sut.NotificationEnabledCommand).Execute(null);

        Assert.False(sut.NotificationEnabled);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.False(update.Schedule.NotificationEnabled);
        Assert.False(update.ShouldSave);
    }

    [Fact]
    public void NotificationEnabled_setter_skips_dispatch_when_schedule_missing()
    {
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSutWithoutSchedule(dispatcher);

        sut.NotificationEnabled = true;

        Assert.True(sut.NotificationEnabled);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task OpenModalCommand_opens_number_of_tracks_modal()
    {
        var navigation = new RecordingNavigationService();
        var state = new MutableApplicationState(App(Schedule()));
        using var sut = CreateSut(state, navigation: navigation);

        await ExecuteAsync(sut.OpenModalCommand);

        Assert.Equal(1, navigation.OpenNumberOfTracksModalCalls);
        Assert.Same(sut, navigation.LastOpenBindingContext);
    }

    [Fact]
    public async Task CloseModalCommand_pops_modal()
    {
        var navigation = new RecordingNavigationService();
        var state = new MutableApplicationState(App(Schedule()));
        using var sut = CreateSut(state, navigation: navigation);

        await ExecuteAsync(sut.CloseModalCommand);

        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task OverlayCancelCommand_pops_modal_and_clears_cancel_busy()
    {
        var navigation = new RecordingNavigationService();
        var state = new MutableApplicationState(App(Schedule()));
        using var sut = CreateSut(state, navigation: navigation);

        await ExecuteAsync(sut.OverlayCancelCommand);

        Assert.Equal(1, navigation.PopModalCalls);
        Assert.False(sut.IsCancelBusy);
    }

    [Fact]
    public async Task SelectNumberOfTracksCommand_selects_item_dispatches_and_pops()
    {
        var navigation = new RecordingNavigationService();
        var state = new MutableApplicationState(App(Schedule(numberOfTracksToPlay: 1)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher, navigation);
        await sut.PopulateNumberOfTracksListViewAsync(1);
        var previous = sut.CurrentNumberOfTracks;
        Assert.NotNull(previous);
        var next = sut.NumberOfTracksList.First(x => x.Value == 4);
        dispatcher.Dispatched.Clear();

        await ExecuteAsync(sut.SelectNumberOfTracksCommand, next);

        Assert.False(previous!.IsSelected);
        Assert.True(next.IsSelected);
        Assert.Same(next, sut.CurrentNumberOfTracks);
        Assert.Same(next, sut.SelectedItem);
        Assert.Equal("4", sut.SelectedNumberText);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal(4, update.Schedule.NumberOfTracksToPlay);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task PopulateNumberOfTracksListViewAsync_builds_list_and_selects_forced_value()
    {
        var state = new MutableApplicationState(App(Schedule(numberOfTracksToPlay: 2, categoryName: "Bible")));
        using var sut = CreateSut(state);

        await sut.PopulateNumberOfTracksListViewAsync(7);

        Assert.Equal(21, sut.NumberOfTracksList.Count);
        Assert.NotNull(sut.CurrentNumberOfTracks);
        Assert.Equal(7, sut.CurrentNumberOfTracks!.Value);
        Assert.True(sut.CurrentNumberOfTracks.IsSelected);
        Assert.Contains("7", sut.CurrentNumberOfTracksText);
        Assert.Equal(sut.CurrentNumberOfTracks, sut.SelectedItem);
    }

    [Fact]
    public async Task PopulateNumberOfTracksListViewAsync_defaults_selection_from_schedule()
    {
        var state = new MutableApplicationState(App(Schedule(numberOfTracksToPlay: 5)));
        using var sut = CreateSut(state);

        await sut.PopulateNumberOfTracksListViewAsync();

        Assert.Equal(5, sut.CurrentNumberOfTracks?.Value);
    }

    [Fact]
    public void Label_text_properties_reflect_category()
    {
        var state = new MutableApplicationState(App(Schedule(categoryName: "Bible")));
        using var sut = CreateSut(state);

        Assert.Equal("Chapters to play each time", sut.TrackLabelText);
        Assert.Equal("Number of chapters to play", sut.TracksLabelText);
        Assert.Equal("Select Number of Chapters", sut.ModalHeaderText);
        Assert.Contains("chapter", sut.RestartLabelText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectedTracksText_uses_current_selection_value()
    {
        var state = new MutableApplicationState(App(Schedule(numberOfTracksToPlay: 0)));
        using var sut = CreateSut(state);
        sut.CurrentNumberOfTracks = new NumberOfTracksListViewItemModel(2, "chapter", "chapters");

        Assert.Equal("2 Chapters", sut.SelectedTracksText);
        Assert.Equal("2", sut.SelectedNumberText);
    }

    [Fact]
    public void OnStateChanged_when_same_schedule_syncs_property_flags()
    {
        var current = Schedule(alwaysPlayFromStart: false, numberOfTracksToPlay: 2, notificationEnabled: true);
        var state = new MutableApplicationState(App(current));
        using var sut = CreateSut(state);

        state.Value.CurrentSchedule = Schedule(
            id: current.Id,
            alwaysPlayFromStart: true,
            numberOfTracksToPlay: 0,
            notificationEnabled: false);
        state.NotifyChanged();

        Assert.True(sut.AlwaysPlayFromStart);
        Assert.True(sut.PlayIndefinitely);
        Assert.False(sut.NotificationEnabled);
    }

    [Fact]
    public void OnStateChanged_when_schedule_id_changes_reinitializes()
    {
        var state = new MutableApplicationState(App(Schedule(id: 1, alwaysPlayFromStart: false, notificationEnabled: true)));
        using var sut = CreateSut(state);

        state.Value.CurrentSchedule = Schedule(id: 2, alwaysPlayFromStart: true, notificationEnabled: false, numberOfTracksToPlay: 0);
        state.NotifyChanged();

        Assert.True(sut.AlwaysPlayFromStart);
        Assert.False(sut.NotificationEnabled);
        Assert.True(sut.PlayIndefinitely);
    }

    [Fact]
    public void IsBusy_property_round_trips()
    {
        using var sut = CreateSutWithoutSchedule(new RecordingDispatcher());

        sut.IsBusy = false;

        Assert.False(sut.IsBusy);
    }

    [Fact]
    public void Dispose_then_state_changed_does_not_throw()
    {
        var state = new MutableApplicationState(App(Schedule()));
        var sut = CreateSut(state);
        sut.Dispose();

        state.Value.CurrentSchedule = Schedule(notificationEnabled: false);
        state.NotifyChanged();
    }
}
