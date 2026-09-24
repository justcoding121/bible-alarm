#nullable enable

using System.Reflection;
using System.Runtime.InteropServices;
using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
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

public sealed class ScheduleDetailsContainerViewModelTests
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

    private sealed class RecordingScheduleValidationService : IScheduleValidationService
    {
        public List<WeekDays> Validated { get; } = [];

        public Task<bool> ValidateDaysOfWeekAsync(WeekDays daysOfWeek)
        {
            Validated.Add(daysOfWeek);
            return Task.FromResult(false);
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
        string name = "Morning",
        bool isEnabled = true,
        int hour = 7,
        int minute = 30,
        int second = 0,
        WeekDays daysOfWeek = WeekDays.Monday) =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = isEnabled,
            Hour = hour,
            Minute = minute,
            Second = second,
            DaysOfWeek = daysOfWeek,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
        };

    /// <summary>
    /// Pre-marks ScheduleDetails ready so SignalContainerReady skips MainThread.BeginInvokeOnMainThread
    /// (throws COMException on headless Windows without a WinUI dispatcher).
    /// </summary>
    private static ApplicationState App(ScheduleStateItem? current) =>
        new(
            new ObservableHashSet<ScheduleStateItem>(),
            current,
            containerReadiness: new ContainerReadiness { ScheduleDetails = true });

    private static ScheduleDetailsContainerViewModel CreateSut(
        MutableApplicationState state,
        RecordingDispatcher? dispatcher = null,
        IScheduleValidationService? validation = null)
    {
        dispatcher ??= new RecordingDispatcher();
        return new ScheduleDetailsContainerViewModel(
            TestLogging.CreateLogger(),
            state,
            dispatcher,
            CreateMapper(),
            validation ?? new RecordingScheduleValidationService());
    }

    /// <summary>
    /// Marks the private hasSignaledReady flag so OnStateChanged reaches SyncScheduleFieldsAfterInitialization
    /// without calling MainThread (which is unavailable under plain dotnet test).
    /// </summary>
    private static void MarkHasSignaledReady(ScheduleDetailsContainerViewModel sut)
    {
        var field = typeof(ScheduleDetailsContainerViewModel).GetField(
            "hasSignaledReady",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(sut, true);
    }

    private static async Task ExecuteToggleDayAsync(System.Windows.Input.ICommand command, object? parameter)
    {
        if (command is IAsyncRelayCommand<object> typed)
        {
            await typed.ExecuteAsync(parameter);
            return;
        }

        throw new InvalidOperationException("Expected IAsyncRelayCommand<object>");
    }

    [Fact]
    public void Ctor_does_not_require_current_schedule()
    {
        using var sut = CreateSut(new MutableApplicationState(App(null)));
        Assert.NotNull(sut);
        Assert.Equal(TimeSpan.Zero, sut.Time);
        Assert.Equal(string.Empty, sut.Name);
        Assert.False(sut.IsEnabled);
        Assert.Equal(default(WeekDays), sut.DaysOfWeek);
        Assert.NotNull(sut.ToggleDayCommand);
    }

    [Fact]
    public void Constructor_loads_schedule_details_from_current_schedule()
    {
        var state = new MutableApplicationState(App(Schedule(
            name: "Dawn",
            isEnabled: false,
            hour: 6,
            minute: 15,
            second: 45,
            daysOfWeek: WeekDays.Monday | WeekDays.Friday)));
        using var sut = CreateSut(state);

        Assert.Equal(new TimeSpan(6, 15, 45), sut.Time);
        Assert.Equal("Dawn", sut.Name);
        Assert.False(sut.IsEnabled);
        Assert.Equal(WeekDays.Monday | WeekDays.Friday, sut.DaysOfWeek);
    }

    [Fact]
    public void Time_setter_dispatches_update_when_value_changes()
    {
        var state = new MutableApplicationState(App(Schedule(hour: 7, minute: 0)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        sut.Time = new TimeSpan(8, 45, 10);

        Assert.Equal(new TimeSpan(8, 45, 10), sut.Time);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal(8, update.Schedule.Hour);
        Assert.Equal(45, update.Schedule.Minute);
        Assert.Equal(10, update.Schedule.Second);
        Assert.False(update.ShouldSave);
    }

    [Fact]
    public void Time_setter_does_not_dispatch_when_value_unchanged()
    {
        var state = new MutableApplicationState(App(Schedule(hour: 7, minute: 30)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        sut.Time = new TimeSpan(7, 30, 0);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void Name_setter_dispatches_update_when_value_changes()
    {
        var state = new MutableApplicationState(App(Schedule(name: "Old")));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        sut.Name = "New";

        Assert.Equal("New", sut.Name);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal("New", update.Schedule.Name);
        Assert.False(update.ShouldSave);
    }

    [Fact]
    public void IsEnabled_setter_dispatches_update_when_value_changes()
    {
        var state = new MutableApplicationState(App(Schedule(isEnabled: true)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        sut.IsEnabled = false;

        Assert.False(sut.IsEnabled);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.False(update.Schedule.IsEnabled);
    }

    [Fact]
    public void DaysOfWeek_setter_dispatches_update_when_value_changes()
    {
        var state = new MutableApplicationState(App(Schedule(daysOfWeek: WeekDays.Monday)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        sut.DaysOfWeek = WeekDays.Tuesday | WeekDays.Thursday;

        Assert.Equal(WeekDays.Tuesday | WeekDays.Thursday, sut.DaysOfWeek);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal(WeekDays.Tuesday | WeekDays.Thursday, update.Schedule.DaysOfWeek);
    }

    [Fact]
    public void Property_setters_do_not_dispatch_when_current_schedule_is_missing()
    {
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(new MutableApplicationState(App(null)), dispatcher);
        dispatcher.Dispatched.Clear();

        sut.Time = new TimeSpan(9, 0, 0);
        sut.Name = "Solo";
        sut.IsEnabled = true;
        sut.DaysOfWeek = WeekDays.Sunday;

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(new TimeSpan(9, 0, 0), sut.Time);
        Assert.Equal("Solo", sut.Name);
        Assert.True(sut.IsEnabled);
        Assert.Equal(WeekDays.Sunday, sut.DaysOfWeek);
    }

    [Fact]
    public async Task ToggleDayCommand_adds_day_when_not_selected()
    {
        var state = new MutableApplicationState(App(Schedule(daysOfWeek: WeekDays.Monday)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        await ExecuteToggleDayAsync(sut.ToggleDayCommand, WeekDays.Wednesday);

        Assert.Equal(WeekDays.Monday | WeekDays.Wednesday, sut.DaysOfWeek);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal(WeekDays.Monday | WeekDays.Wednesday, update.Schedule.DaysOfWeek);
    }

    [Fact]
    public async Task ToggleDayCommand_removes_day_when_multiple_selected()
    {
        var state = new MutableApplicationState(App(Schedule(daysOfWeek: WeekDays.Monday | WeekDays.Friday)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        await ExecuteToggleDayAsync(sut.ToggleDayCommand, WeekDays.Friday);

        Assert.Equal(WeekDays.Monday, sut.DaysOfWeek);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal(WeekDays.Monday, update.Schedule.DaysOfWeek);
    }

    [Fact]
    public async Task ToggleDayCommand_accepts_day_name_string()
    {
        var state = new MutableApplicationState(App(Schedule(daysOfWeek: WeekDays.Monday)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        await ExecuteToggleDayAsync(sut.ToggleDayCommand, "Tuesday");

        Assert.Equal(WeekDays.Monday | WeekDays.Tuesday, sut.DaysOfWeek);
    }

    [Fact]
    public async Task ToggleDayCommand_ignores_invalid_parameter()
    {
        var state = new MutableApplicationState(App(Schedule(daysOfWeek: WeekDays.Monday)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        dispatcher.Dispatched.Clear();

        await ExecuteToggleDayAsync(sut.ToggleDayCommand, 42);
        await ExecuteToggleDayAsync(sut.ToggleDayCommand, "NotADay");
        await ExecuteToggleDayAsync(sut.ToggleDayCommand, null);

        Assert.Equal(WeekDays.Monday, sut.DaysOfWeek);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task ToggleDayCommand_blocks_deselecting_last_day_and_validates()
    {
        var state = new MutableApplicationState(App(Schedule(daysOfWeek: WeekDays.Monday)));
        var dispatcher = new RecordingDispatcher();
        var validation = new RecordingScheduleValidationService();
        using var sut = CreateSut(state, dispatcher, validation);
        dispatcher.Dispatched.Clear();

        await ExecuteToggleDayAsync(sut.ToggleDayCommand, WeekDays.Monday);

        Assert.Equal(WeekDays.Monday, sut.DaysOfWeek);
        Assert.Empty(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal([default(WeekDays)], validation.Validated);
    }

    [Fact]
    public void OnStateChanged_when_same_schedule_syncs_fields_from_store()
    {
        var current = Schedule(id: 6, name: "A", isEnabled: true, hour: 7, minute: 0, daysOfWeek: WeekDays.Monday);
        var state = new MutableApplicationState(App(current));
        using var sut = CreateSut(state);
        MarkHasSignaledReady(sut);

        state.Value.CurrentSchedule = Schedule(
            id: 6,
            name: "B",
            isEnabled: false,
            hour: 9,
            minute: 15,
            second: 20,
            daysOfWeek: WeekDays.Friday);
        state.NotifyChanged();

        Assert.Equal("B", sut.Name);
        Assert.False(sut.IsEnabled);
        Assert.Equal(new TimeSpan(9, 15, 20), sut.Time);
        Assert.Equal(WeekDays.Friday, sut.DaysOfWeek);
    }

    [Fact]
    public void OnStateChanged_when_state_days_zero_preserves_local_and_dispatches_fix()
    {
        var state = new MutableApplicationState(App(Schedule(daysOfWeek: WeekDays.Monday | WeekDays.Wednesday)));
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(state, dispatcher);
        MarkHasSignaledReady(sut);
        dispatcher.Dispatched.Clear();

        state.Value.CurrentSchedule = Schedule(daysOfWeek: 0);
        state.NotifyChanged();

        Assert.Equal(WeekDays.Monday | WeekDays.Wednesday, sut.DaysOfWeek);
        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal(WeekDays.Monday | WeekDays.Wednesday, update.Schedule.DaysOfWeek);
    }

    [Fact]
    public void OnStateChanged_when_schedule_id_changes_reinitializes_from_store()
    {
        var state = new MutableApplicationState(App(Schedule(id: 1, name: "First", isEnabled: true)));
        using var sut = CreateSut(state);

        state.Value.CurrentSchedule = Schedule(id: 2, name: "Second", isEnabled: false, hour: 10, minute: 0);
        state.NotifyChanged();

        Assert.Equal("Second", sut.Name);
        Assert.False(sut.IsEnabled);
        Assert.Equal(new TimeSpan(10, 0, 0), sut.Time);
    }

    [Fact]
    public void OnStateChanged_when_schedule_appears_initializes_from_store()
    {
        var state = new MutableApplicationState(App(null));
        using var sut = CreateSut(state);

        state.Value.CurrentSchedule = Schedule(id: 11, name: "Appeared", hour: 5, minute: 5);
        state.NotifyChanged();

        Assert.Equal("Appeared", sut.Name);
        Assert.Equal(new TimeSpan(5, 5, 0), sut.Time);
    }

    [Fact]
    public void OnStateChanged_when_readiness_reset_reinitializes_from_store()
    {
        var state = new MutableApplicationState(App(Schedule(id: 3, name: "Ready")));
        using var sut = CreateSut(state);
        MarkHasSignaledReady(sut);

        // Simulate ResetContainerReadinessAction: clear readiness while schedule remains.
        state.Value.ContainerReadiness = ContainerReadiness.NotReady;
        state.Value.CurrentSchedule = Schedule(id: 3, name: "AfterReset", hour: 11, minute: 11);

        try
        {
            state.NotifyChanged();
        }
        catch (COMException)
        {
            // Headless host: SignalContainerReady after reset hits MainThread; fields already loaded.
        }

        Assert.Equal("AfterReset", sut.Name);
        Assert.Equal(new TimeSpan(11, 11, 0), sut.Time);
    }

    [Fact]
    public void Dispose_then_state_changed_does_not_throw()
    {
        var state = new MutableApplicationState(App(Schedule(8)));
        var sut = CreateSut(state);
        sut.Dispose();

        state.Value.CurrentSchedule = Schedule(8, name: "Gone");
        state.NotifyChanged();
    }
}
