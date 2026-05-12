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

        public void Dispose()
        {
        }

        public Task ShowMessage(string message, int seconds = 3)
        {
            Messages.Add((message, seconds));
            return Task.CompletedTask;
        }

        public Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3) => Task.CompletedTask;

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

    private sealed class StubPlaybackService : IPlaybackService
    {
        public bool IsAlarmPlaybackSession => false;

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
        public Task StopAsync() => Task.CompletedTask;
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
            new StubPlaybackService(),
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
            new StubPlaybackService(),
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
            new StubPlaybackService(),
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
            new StubPlaybackService(),
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
            new StubPlaybackService(),
            new StubNotificationService(),
            new RecordingToast(),
            CreateMapper(),
            state));

        await sut.ExecuteCancelAsync(isNewSchedule: true, scheduleId: -2, Row(-2));

        Assert.Contains(dispatcher.Dispatched, a => a is RemoveScheduleSuccessAction r && r.ScheduleId == -2);
        Assert.Contains(dispatcher.Dispatched, a => a is ResetScheduleStateAction);
        Assert.Equal(1, nav.NavigateHomeCalls);
    }
}
