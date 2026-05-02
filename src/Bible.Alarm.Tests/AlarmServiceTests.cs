#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Tests;

public sealed class AlarmServiceTests
{
    private sealed class FakeNotificationService : INotificationService
    {
        public bool CanSchedule { get; set; } = true;
        public List<(int ScheduleId, string Title, string Body)> Scheduled { get; } = [];
        public List<int> Removed { get; } = [];

        public Task ShowNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule, string title, string body)
        {
            Scheduled.Add((alarmSchedule.Id, title, body));
            return Task.CompletedTask;
        }

        public Task RemoveAsync(int scheduleId)
        {
            Removed.Add(scheduleId);
            return Task.CompletedTask;
        }

        public Task<bool> IsScheduledAsync(int scheduleId) =>
            Task.FromResult(false);

        public Task ClearDeliveredNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> CanScheduleAsync() => Task.FromResult(CanSchedule);
    }

    private static AlarmSchedule MinimalSchedule(
        int id = 1,
        string name = "Morning",
        bool isEnabled = true,
        WeekDays daysOfWeek = WeekDays.Monday) =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = isEnabled,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = daysOfWeek,
            NotificationEnabled = true,
            MusicEnabled = true,
            CurrentPlayItem = PlayType.Music,
            LatestAlarmNotificationId = 0,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 0,
            AlwaysPlayFromStart = false
        };

    [Fact]
    public async Task Create_WhenCannotSchedule_DoesNotSchedule()
    {
        var notifications = new FakeNotificationService { CanSchedule = false };
        var svc = new AlarmService(notifications);

        await svc.Create(MinimalSchedule());

        Assert.Empty(notifications.Scheduled);
    }

    [Fact]
    public async Task Create_WhenDisabled_DoesNotSchedule()
    {
        var notifications = new FakeNotificationService();
        var svc = new AlarmService(notifications);

        await svc.Create(MinimalSchedule(isEnabled: false));

        Assert.Empty(notifications.Scheduled);
    }

    [Fact]
    public async Task Create_WhenNoDaysSelected_DoesNotSchedule()
    {
        var notifications = new FakeNotificationService();
        var svc = new AlarmService(notifications);

        await svc.Create(MinimalSchedule(daysOfWeek: 0));

        Assert.Empty(notifications.Scheduled);
    }

    [Fact]
    public async Task Create_WhenEligible_SchedulesWithNameAndConstantBody()
    {
        var notifications = new FakeNotificationService();
        var svc = new AlarmService(notifications);
        var schedule = MinimalSchedule(name: "Custom");

        await svc.Create(schedule);

        var call = Assert.Single(notifications.Scheduled);
        Assert.Equal(schedule.Id, call.ScheduleId);
        Assert.Equal("Custom", call.Title);
        Assert.Equal(AppConstants.Notifications.TapAlarmToListenBody, call.Body);
    }

    [Fact]
    public async Task Create_WhenNameIsWhitespace_UsesEmptyTitle()
    {
        var notifications = new FakeNotificationService();
        var svc = new AlarmService(notifications);

        await svc.Create(MinimalSchedule(name: "   "));

        var call = Assert.Single(notifications.Scheduled);
        Assert.Equal(string.Empty, call.Title);
    }

    [Fact]
    public async Task Update_WhenCanSchedule_RemovesThenSchedulesWhenEligible()
    {
        var notifications = new FakeNotificationService();
        var svc = new AlarmService(notifications);
        var schedule = MinimalSchedule(id: 42);

        await svc.Update(schedule);

        Assert.Equal(42, Assert.Single(notifications.Removed));
        Assert.Single(notifications.Scheduled);
    }

    [Fact]
    public async Task Update_WhenCannotSchedule_DoesNotRemoveOrSchedule()
    {
        var notifications = new FakeNotificationService { CanSchedule = false };
        var svc = new AlarmService(notifications);

        await svc.Update(MinimalSchedule());

        Assert.Empty(notifications.Removed);
        Assert.Empty(notifications.Scheduled);
    }

    [Fact]
    public async Task Update_WhenDisabledAfterRemove_DoesNotReschedule()
    {
        var notifications = new FakeNotificationService();
        var svc = new AlarmService(notifications);

        await svc.Update(MinimalSchedule(isEnabled: false));

        Assert.Single(notifications.Removed);
        Assert.Empty(notifications.Scheduled);
    }

    [Fact]
    public async Task Delete_WhenCanSchedule_Removes()
    {
        var notifications = new FakeNotificationService();
        var svc = new AlarmService(notifications);

        await svc.Delete(99);

        Assert.Equal(99, Assert.Single(notifications.Removed));
    }

    [Fact]
    public async Task Delete_WhenCannotSchedule_DoesNotRemove()
    {
        var notifications = new FakeNotificationService { CanSchedule = false };
        var svc = new AlarmService(notifications);

        await svc.Delete(1);

        Assert.Empty(notifications.Removed);
    }
}
