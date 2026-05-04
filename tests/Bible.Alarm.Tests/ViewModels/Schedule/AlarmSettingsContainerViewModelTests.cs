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
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class AlarmSettingsContainerViewModelTests
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

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class UnusedNavigationService : INavigationService
    {
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
        public Task PopModalAsync() => Task.CompletedTask;
        public Task PopAsync() => Task.CompletedTask;
        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;
        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Views.Home? GetCurrentHomePage() => null;
        public Microsoft.Maui.Controls.Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;
        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleStateItem Schedule(int id, bool isEnabled = true, bool notificationEnabled = true) =>
        new()
        {
            Id = id,
            Name = "Test",
            IsEnabled = isEnabled,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = notificationEnabled,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
        };

    private static ApplicationState App(ScheduleStateItem? current) =>
        new(new ObservableHashSet<ScheduleStateItem>(), current);

    private static AlarmSettingsContainerViewModel CreateSut(
        MutableApplicationState state,
        RecordingDispatcher? dispatcher = null)
    {
        dispatcher ??= new RecordingDispatcher();
        return new AlarmSettingsContainerViewModel(
            TestLogging.CreateLogger(),
            new UnusedNavigationService(),
            new EmptyServiceProvider(),
            state,
            dispatcher,
            CreateMapper());
    }

    [Fact]
    public void Constructor_LoadsIsEnabledAndNotificationFromCurrentSchedule()
    {
        var current = Schedule(9, isEnabled: false, notificationEnabled: false);
        var state = new MutableApplicationState(App(current));
        using var sut = CreateSut(state);

        Assert.False(sut.IsEnabled);
        Assert.False(sut.NotificationEnabled);
    }

    [Fact]
    public void ToggleEnabledCommand_FlipsValue_AndDispatchesUpdate()
    {
        var current = Schedule(3, isEnabled: true);
        var state = new MutableApplicationState(App(current));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);

        ((RelayCommand)sut.ToggleEnabledCommand).Execute(null);

        Assert.False(sut.IsEnabled);
        var action = Assert.Single(dispatcher.Dispatched);
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
        Assert.False(update.Schedule.IsEnabled);
        Assert.False(update.ShouldSave);
    }

    [Fact]
    public void ToggleNotificationCommand_FlipsValue_AndDispatchesUpdate()
    {
        var current = Schedule(4, notificationEnabled: true);
        var state = new MutableApplicationState(App(current));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);

        ((RelayCommand)sut.ToggleNotificationEnabledCommand).Execute(null);

        Assert.False(sut.NotificationEnabled);
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(Assert.Single(dispatcher.Dispatched));
        Assert.False(update.Schedule.NotificationEnabled);
    }

    [Fact]
    public void OnStateChanged_WhenSameSchedule_NotificationDiffers_SyncsFromStore()
    {
        var current = Schedule(5, isEnabled: true, notificationEnabled: false);
        var state = new MutableApplicationState(App(current));
        using var sut = CreateSut(state);

        state.Value.CurrentSchedule = Schedule(5, isEnabled: true, notificationEnabled: true);
        state.NotifyChanged();

        Assert.True(sut.NotificationEnabled);
    }

    [Fact]
    public void OnStateChanged_WhenSameSchedule_IsEnabledDiffers_SyncsFromStore()
    {
        var current = Schedule(6, isEnabled: true);
        var state = new MutableApplicationState(App(current));
        using var sut = CreateSut(state);

        state.Value.CurrentSchedule = Schedule(6, isEnabled: false);
        state.NotifyChanged();

        Assert.False(sut.IsEnabled);
    }

    [Fact]
    public void OnStateChanged_WhenScheduleIdChanges_ReinitializesFromStore()
    {
        var state = new MutableApplicationState(App(Schedule(1, isEnabled: true)));
        using var sut = CreateSut(state);

        state.Value.CurrentSchedule = Schedule(2, isEnabled: false, notificationEnabled: false);
        state.NotifyChanged();

        Assert.False(sut.IsEnabled);
        Assert.False(sut.NotificationEnabled);
    }

    [Fact]
    public void Dispose_ThenStateChanged_DoesNotThrow()
    {
        var state = new MutableApplicationState(App(Schedule(8)));
        var sut = CreateSut(state);
        sut.Dispose();

        state.Value.CurrentSchedule = Schedule(8, notificationEnabled: false);
        state.NotifyChanged();
    }
}
