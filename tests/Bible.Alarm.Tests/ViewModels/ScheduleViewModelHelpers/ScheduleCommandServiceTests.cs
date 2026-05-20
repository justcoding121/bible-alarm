#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Devices;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleCommandServiceTests
{
    private sealed class FakeAppState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class MutableAppState : IState<ApplicationState>
    {
        private ApplicationState value;

        public MutableAppState(ApplicationState initial) => value = initial;

        public ApplicationState Value => value;

        public event EventHandler? StateChanged;

        public void SetState(ApplicationState newState)
        {
            value = newState;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class SimulatingDispatcher(Action<object>? onDispatch = null) : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            onDispatch?.Invoke(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
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

    private sealed class RecordingNav : INavigationService
    {
        public int NavigateHomeCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task NavigateToHomeAsync(bool animated = true)
        {
            NavigateHomeCalls++;
            return Task.CompletedTask;
        }

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

    private sealed class RecordingToast : IToastService
    {
        public List<(string Message, int Seconds)> Messages { get; } = [];
        public int ScheduledCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task ShowMessage(string message, int seconds = 3)
        {
            Messages.Add((message, seconds));
            return Task.CompletedTask;
        }

        public Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3)
        {
            ScheduledCalls++;
            return Task.CompletedTask;
        }

        public Task Clear() => Task.CompletedTask;
    }

    private sealed class StubNotificationService : INotificationService
    {
        public Task ShowNotificationAsync(int scheduleId) => Task.CompletedTask;
        public Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule, string title, string body) => Task.CompletedTask;
        public Task RemoveAsync(int scheduleId) => Task.CompletedTask;
        public Task<bool> IsScheduledAsync(int scheduleId) => Task.FromResult(false);
        public Task ClearDeliveredNotificationAsync(int scheduleId) => Task.CompletedTask;
        public Task<bool> CanScheduleAsync() => Task.FromResult(true);
    }

    private sealed class ThrowingScheduleSaveService : IScheduleSaveService
    {
        public Task<AlarmSchedule> PrepareModelForSaveAsync(ScheduleStateItem currentSchedule, bool isNewSchedule, bool musicUpdated) =>
            throw new InvalidOperationException("save should not run");
        public ScheduleStateItem PrepareScheduleStateItem(AlarmSchedule model, ScheduleStateItem currentSchedule, bool musicUpdated) =>
            throw new InvalidOperationException("save should not run");
    }

    private sealed class StubScheduleSaveService : IScheduleSaveService
    {
        public Task<AlarmSchedule> PrepareModelForSaveAsync(ScheduleStateItem currentSchedule, bool isNewSchedule, bool musicUpdated) =>
            Task.FromResult(new AlarmSchedule
            {
                Id = currentSchedule.Id,
                Name = currentSchedule.Name,
                IsEnabled = currentSchedule.IsEnabled,
                Hour = currentSchedule.Hour,
                Minute = currentSchedule.Minute,
                DaysOfWeek = currentSchedule.DaysOfWeek,
            });

        public ScheduleStateItem PrepareScheduleStateItem(AlarmSchedule model, ScheduleStateItem currentSchedule, bool musicUpdated) =>
            new()
            {
                Id = model.Id,
                Name = model.Name,
                IsEnabled = model.IsEnabled,
                Hour = currentSchedule.Hour,
                Minute = currentSchedule.Minute,
                Second = currentSchedule.Second,
                DaysOfWeek = currentSchedule.DaysOfWeek,
                NotificationEnabled = currentSchedule.NotificationEnabled,
                MusicEnabled = currentSchedule.MusicEnabled,
                SnoozeMinutes = currentSchedule.SnoozeMinutes,
                NumberOfTracksToPlay = currentSchedule.NumberOfTracksToPlay,
                AlwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart,
                CurrentPlayItem = currentSchedule.CurrentPlayItem,
                BiblePublicationFinishedDuration = currentSchedule.BiblePublicationFinishedDuration,
            };
    }

    private sealed class DenyNotificationSchedulingService : INotificationService
    {
        public Task ShowNotificationAsync(int scheduleId) => Task.CompletedTask;
        public Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule, string title, string body) => Task.CompletedTask;
        public Task RemoveAsync(int scheduleId) => Task.CompletedTask;
        public Task<bool> IsScheduledAsync(int scheduleId) => Task.FromResult(false);
        public Task ClearDeliveredNotificationAsync(int scheduleId) => Task.CompletedTask;
        public Task<bool> CanScheduleAsync() => Task.FromResult(false);
    }

    private sealed class RecordingPlaybackService : IPlaybackService
    {
        public bool IsAlarmPlaybackSession => false;
        public int StopCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task PlayAsync() => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task PlayPreviousAsync() => Task.CompletedTask;
        public Task PlayNextAsync() => Task.CompletedTask;
        public Task SeekForwardAsync() => Task.CompletedTask;
        public Task SeekBackwardAsync() => Task.CompletedTask;
        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;
        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm) => Task.CompletedTask;
        public Task StopAsync()
        {
            StopCalls++;
            return Task.CompletedTask;
        }
        public Task StopForTeardownAsync() => Task.CompletedTask;
        public Task ResetAndRetryAsync(int scheduleId) => Task.CompletedTask;
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleStateItem Row(int id) =>
        new()
        {
            Id = id,
            Name = "Test",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = false,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
        };

    [Fact]
    public async Task ExecuteSaveAsync_returns_false_and_toasts_when_model_not_initialized()
    {
        var toast = new RecordingToast();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            new RecordingDispatcher(),
            new RecordingNav(),
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            toast,
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        var ok = await sut.ExecuteSaveAsync(false, 1, Row(1), false, false, modelInitialized: false);

        Assert.False(ok);
        var msg = Assert.Single(toast.Messages);
        Assert.Equal(AppConstants.ToastMessages.ScheduleDataNotReadyTryAgain, msg.Message);
    }

    [Fact]
    public async Task ExecuteSaveAsync_returns_false_when_no_days_selected()
    {
        var toast = new RecordingToast();
        var schedule = Row(1);
        schedule.DaysOfWeek = 0;
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            new RecordingDispatcher(),
            new RecordingNav(),
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            toast,
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        var ok = await sut.ExecuteSaveAsync(false, 1, schedule, false, false, modelInitialized: true);

        Assert.False(ok);
        Assert.Equal(AppConstants.ToastMessages.SelectAtLeastOneDay, toast.Messages[0].Message);
    }

    [Fact]
    public async Task ExecuteSaveAsync_returns_false_for_existing_schedule_with_non_positive_id()
    {
        var toast = new RecordingToast();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            new RecordingDispatcher(),
            new RecordingNav(),
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            toast,
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        var ok = await sut.ExecuteSaveAsync(isNewSchedule: false, scheduleId: 0, Row(0), false, false, modelInitialized: true);

        Assert.False(ok);
        Assert.Equal(AppConstants.ToastMessages.InvalidScheduleIdTryAgain, toast.Messages[0].Message);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_hides_overlay_without_navigating_when_schedule_id_invalid()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            new RecordingNav(),
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        var ok = await sut.ExecuteDeleteAsync(isNewSchedule: false, scheduleId: 0, scheduleCount: 3);

        Assert.False(ok);
        var overlay = Assert.IsType<SetSchedulePageOverlayAction>(Assert.Single(dispatcher.Dispatched));
        Assert.False(overlay.IsVisible);
    }

    [Fact]
    public async Task ExecuteCancelAsync_removes_unsaved_schedules_from_state_for_new_schedule()
    {
        var dispatcher = new RecordingDispatcher();
        var nav = new RecordingNav();
        var schedules = new ObservableHashSet<ScheduleStateItem> { Row(-2), Row(3) };
        var state = new FakeAppState(new ApplicationState(schedules, currentSchedule: Row(-2)));
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            nav,
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            state));

        await sut.ExecuteCancelAsync(isNewSchedule: true, scheduleId: -2, Row(-2));

        Assert.Contains(dispatcher.Dispatched, a => a is RemoveScheduleSuccessAction r && r.ScheduleId == -2);
        Assert.Contains(dispatcher.Dispatched, a => a is ResetScheduleStateAction);
        Assert.Equal(1, nav.NavigateHomeCalls);
    }

    [Fact]
    public async Task ExecuteCancelAsync_existing_schedule_resets_state_and_navigates_home()
    {
        var dispatcher = new RecordingDispatcher();
        var nav = new RecordingNav();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            nav,
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { Row(4) }))));

        await sut.ExecuteCancelAsync(isNewSchedule: false, scheduleId: 4, Row(4));

        Assert.Contains(dispatcher.Dispatched, a => a is ResetScheduleStateAction);
        Assert.Contains(dispatcher.Dispatched, a => a is SetSchedulePageOverlayAction { IsVisible: false });
        Assert.Equal(1, nav.NavigateHomeCalls);
        Assert.DoesNotContain(dispatcher.Dispatched, a => a is RemoveScheduleSuccessAction);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_blocks_deleting_last_schedule_and_hides_overlay()
    {
        var dispatcher = new RecordingDispatcher();
        var nav = new RecordingNav();
        var toast = new RecordingToast();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            nav,
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            toast,
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { Row(1) }))));

        var ok = await sut.ExecuteDeleteAsync(isNewSchedule: false, scheduleId: 1, scheduleCount: 1);

        Assert.False(ok);
        Assert.Equal(0, nav.NavigateHomeCalls);
        Assert.Equal(AppConstants.ToastMessages.CannotDeleteLastSchedule, toast.Messages[0].Message);
        Assert.Contains(dispatcher.Dispatched, a => a is SetSchedulePageOverlayAction { IsVisible: false });
        Assert.DoesNotContain(dispatcher.Dispatched, a => a is DeleteScheduleAction);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_dispatches_delete_and_navigates_for_existing_schedule()
    {
        var dispatcher = new RecordingDispatcher();
        var nav = new RecordingNav();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            nav,
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { Row(1), Row(2) }))));

        var ok = await sut.ExecuteDeleteAsync(isNewSchedule: false, scheduleId: 2, scheduleCount: 2);

        Assert.True(ok);
        Assert.Contains(dispatcher.Dispatched, a => a is DeleteScheduleAction d && d.ScheduleId == 2);
        Assert.Contains(dispatcher.Dispatched, a => a is ResetScheduleStateAction);
        Assert.Equal(1, nav.NavigateHomeCalls);
    }

    [Fact]
    public async Task StopPlaybackIfNeededAsync_stops_when_editing_schedule_currently_playing()
    {
        var playback = new RecordingPlaybackService();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            new RecordingDispatcher(),
            new RecordingNav(),
            new ThrowingScheduleSaveService(),
            playback,
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        await sut.StopPlaybackIfNeededAsync(
            isNewSchedule: false,
            isPreparingOrPlaying: true,
            scheduleId: 7,
            currentPlaybackScheduleId: 7);

        Assert.Equal(1, playback.StopCalls);
    }

    [Fact]
    public async Task HandleSaveResultAsync_saved_disabled_shows_saved_toast_without_scheduled_notification()
    {
        var toast = new RecordingToast();
        var nav = new RecordingNav();
        var dispatcher = new RecordingDispatcher();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            nav,
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            toast,
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        await sut.HandleSaveResultAsync(
            saved: true,
            scheduleId: 3,
            isEnabled: false,
            model: new AlarmSchedule { Id = 3, Name = "Off", IsEnabled = false });

        Assert.Equal(1, nav.NavigateHomeCalls);
        Assert.Equal(0, toast.ScheduledCalls);
        Assert.Equal(AppConstants.ToastMessages.ScheduleSaved, toast.Messages[0].Message);
    }

    [Fact]
    public async Task ExecuteSaveAsync_existing_schedule_dispatches_update_with_should_save()
    {
        var dispatcher = new RecordingDispatcher();
        var schedule = Row(5);
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            new RecordingNav(),
            new StubScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem> { Row(5) }))));

        var ok = await sut.ExecuteSaveAsync(false, 5, schedule, musicUpdated: false, biblePublicationUpdated: false, modelInitialized: true);

        Assert.True(ok);
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(
            Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>()));
        Assert.True(update.ShouldSave);
        Assert.Equal(5, update.Schedule.Id);
    }

    [Fact]
    public async Task ExecuteSaveAsync_resets_bible_progress_when_publication_updated()
    {
        var dispatcher = new RecordingDispatcher();
        var schedule = Row(8);
        schedule.BiblePublicationFinishedDuration = TimeSpan.FromMinutes(12);
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            new RecordingNav(),
            new StubScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        await sut.ExecuteSaveAsync(false, 8, schedule, false, biblePublicationUpdated: true, modelInitialized: true);

        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(
            Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>()));
        Assert.Equal(TimeSpan.Zero, update.Schedule.BiblePublicationFinishedDuration);
    }

    [Fact]
    public async Task HandleSaveResultAsync_saved_enabled_shows_scheduled_notification()
    {
        var toast = new RecordingToast();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            new RecordingDispatcher(),
            new RecordingNav(),
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            toast,
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        await sut.HandleSaveResultAsync(
            saved: true,
            scheduleId: 11,
            isEnabled: true,
            model: new AlarmSchedule { Id = 11, Name = "On", IsEnabled = true });

        Assert.Equal(1, toast.ScheduledCalls);
        Assert.Empty(toast.Messages);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_new_schedule_navigates_home_without_delete_action()
    {
        var dispatcher = new RecordingDispatcher();
        var nav = new RecordingNav();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            nav,
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        var ok = await sut.ExecuteDeleteAsync(isNewSchedule: true, scheduleId: -1, scheduleCount: 0);

        Assert.True(ok);
        Assert.Equal(1, nav.NavigateHomeCalls);
        Assert.DoesNotContain(dispatcher.Dispatched, a => a is DeleteScheduleAction);
    }

    [Fact]
    public async Task ValidateNotificationPermissionsAsync_disables_schedule_when_notifications_unavailable()
    {
        if (DeviceInfo.Platform != DevicePlatform.iOS && DeviceInfo.Platform != DevicePlatform.WinUI)
        {
            return;
        }

        var dispatcher = new RecordingDispatcher();
        var schedule = Row(13);
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            new RecordingNav(),
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new DenyNotificationSchedulingService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        await sut.ValidateNotificationPermissionsAsync(schedule);

        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(
            Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>()));
        Assert.False(update.Schedule.IsEnabled);
        Assert.False(update.ShouldSave);
    }

    [Fact]
    public async Task ExecuteSaveAsync_new_schedule_handles_null_schedules_on_state_changed()
    {
        var schedules = new ObservableHashSet<ScheduleStateItem> { Row(1) };
        var appState = new MutableAppState(new ApplicationState(schedules));
        appState.SetState(new ApplicationState(schedules) { Schedules = null! });
        var dispatcher = new SimulatingDispatcher(action =>
        {
            if (action is CreateScheduleAction)
            {
                schedules.Add(Row(50));
                appState.SetState(new ApplicationState(schedules));
            }
        });
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            new RecordingNav(),
            new StubScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            appState));

        var ok = await sut.ExecuteSaveAsync(true, 0, Row(0), false, false, modelInitialized: true);

        Assert.True(ok);
    }

    [Fact]
    public async Task ExecuteSaveAsync_new_schedule_dispatches_create_and_completes_when_state_updates()
    {
        var schedules = new ObservableHashSet<ScheduleStateItem> { Row(1) };
        var appState = new MutableAppState(new ApplicationState(schedules));
        var dispatcher = new SimulatingDispatcher(action =>
        {
            if (action is CreateScheduleAction)
            {
                schedules.Add(Row(50));
                appState.SetState(new ApplicationState(schedules));
            }
        });
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            new RecordingNav(),
            new StubScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            appState));

        var ok = await sut.ExecuteSaveAsync(true, 0, Row(0), false, false, modelInitialized: true);

        Assert.True(ok);
        Assert.Contains(dispatcher.Dispatched, a => a is CreateScheduleAction);
    }

    [Fact]
    public async Task ExecuteSaveAsync_new_disabled_schedule_enables_in_state_before_create()
    {
        var dispatcher = new RecordingDispatcher();
        var schedule = Row(0);
        schedule.IsEnabled = false;
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            new RecordingNav(),
            new StubScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        await sut.ExecuteSaveAsync(true, 0, schedule, false, false, modelInitialized: true);

        var enableUpdate = Assert.Single(
            dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>(),
            a => !a.ShouldSave);
        Assert.True(enableUpdate.Schedule.IsEnabled);
        Assert.Contains(dispatcher.Dispatched, a => a is CreateScheduleAction);
    }

    [Fact]
    public async Task HandleSaveResultAsync_save_failed_hides_overlay_without_navigating()
    {
        var dispatcher = new RecordingDispatcher();
        var nav = new RecordingNav();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            dispatcher,
            nav,
            new ThrowingScheduleSaveService(),
            new RecordingPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        await sut.HandleSaveResultAsync(
            saved: false,
            scheduleId: 4,
            isEnabled: true,
            model: new AlarmSchedule { Id = 4, Name = "Fail", IsEnabled = true });

        Assert.Equal(0, nav.NavigateHomeCalls);
        Assert.Contains(dispatcher.Dispatched, a => a is SetSchedulePageOverlayAction { IsVisible: false });
    }

    [Fact]
    public async Task StopPlaybackIfNeededAsync_skips_when_playback_schedule_differs()
    {
        var playback = new RecordingPlaybackService();
        var sut = new ScheduleCommandService(new ScheduleCommandServiceDeps(
            TestLogging.CreateLogger(),
            new RecordingDispatcher(),
            new RecordingNav(),
            new ThrowingScheduleSaveService(),
            playback,
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            new FakeAppState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()))));

        await sut.StopPlaybackIfNeededAsync(
            isNewSchedule: false,
            isPreparingOrPlaying: true,
            scheduleId: 3,
            currentPlaybackScheduleId: 9);

        Assert.Equal(0, playback.StopCalls);
    }

}
