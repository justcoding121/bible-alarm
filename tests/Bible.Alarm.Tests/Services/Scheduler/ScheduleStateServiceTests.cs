#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStateServiceTests
{
#pragma warning disable CS0067
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) => Dispatched.Add(action);
    }
#pragma warning restore CS0067

    private sealed class StubAlarmScheduleService : IAlarmScheduleService
    {
        public AlarmSchedule? LastUpdated { get; private set; }

        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(new AlarmSchedule
            {
                Id = scheduleId,
                NotificationEnabled = false,
            });

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default)
        {
            var schedule = new AlarmSchedule
            {
                Id = scheduleId,
                Name = "Morning",
                IsEnabled = true,
                Hour = 7,
                Minute = 0,
                DaysOfWeek = WeekDays.Monday,
            };
            updateAction(schedule);
            LastUpdated = schedule;
            return Task.FromResult(schedule);
        }

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmMusic?>(null);

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSchedule?>(null);
    }

    private sealed class RecordingAlarmService : IAlarmService
    {
        public List<AlarmSchedule> Updated { get; } = [];

        public Task Create(AlarmSchedule schedule) => Task.CompletedTask;

        public Task Update(AlarmSchedule schedule)
        {
            Updated.Add(schedule);
            return Task.CompletedTask;
        }

        public Task Delete(int scheduleId) => Task.CompletedTask;
    }

    private sealed class RecordingToastService : IToastService
    {
        public List<AlarmSchedule> ScheduledToasts { get; } = [];

        public Task ShowMessage(string message, int seconds = 3) => Task.CompletedTask;

        public Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3)
        {
            ScheduledToasts.Add(schedule);
            return Task.CompletedTask;
        }

        public Task Clear() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class IdleNotificationService : INotificationService
    {
        public Task ShowNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule, string title, string body) =>
            Task.CompletedTask;

        public Task RemoveAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> IsScheduledAsync(int scheduleId) => Task.FromResult(true);

        public Task ClearDeliveredNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> CanScheduleAsync() => Task.FromResult(true);
    }

    private static ScheduleStateService CreateSut(
        StubAlarmScheduleService schedules,
        RecordingAlarmService alarm,
        RecordingDispatcher dispatcher,
        RecordingToastService toast)
    {
        var deps = new ScheduleStateServiceDeps(
            TestLogging.CreateLogger(),
            schedules,
            alarm,
            new IdleNotificationService(),
            toast,
            dispatcher,
            null!,
            null!);

        return new ScheduleStateService(deps);
    }

    [Fact]
    public async Task UpdateScheduleEnabledStateAsync_disable_updates_database_and_fluxor_without_toast()
    {
        var schedules = new StubAlarmScheduleService();
        var alarm = new RecordingAlarmService();
        var dispatcher = new RecordingDispatcher();
        var toast = new RecordingToastService();
        using var sut = CreateSut(schedules, alarm, dispatcher, toast);

        var result = await sut.UpdateScheduleEnabledStateAsync(5, isEnabled: false);

        Assert.True(result);
        Assert.False(schedules.LastUpdated!.IsEnabled);
        Assert.Equal(schedules.LastUpdated, Assert.Single(alarm.Updated));
        Assert.IsType<UpdateScheduleAction>(Assert.Single(dispatcher.Dispatched));
        Assert.Empty(toast.ScheduledToasts);
    }

    [Fact]
    public async Task UpdateScheduleEnabledStateAsync_enable_shows_scheduled_toast()
    {
        var schedules = new StubAlarmScheduleService();
        var alarm = new RecordingAlarmService();
        var dispatcher = new RecordingDispatcher();
        var toast = new RecordingToastService();
        using var sut = CreateSut(schedules, alarm, dispatcher, toast);

        var result = await sut.UpdateScheduleEnabledStateAsync(8, isEnabled: true);

        Assert.True(result);
        Assert.True(schedules.LastUpdated!.IsEnabled);
        Assert.Equal(8, Assert.Single(toast.ScheduledToasts).Id);
    }
}
