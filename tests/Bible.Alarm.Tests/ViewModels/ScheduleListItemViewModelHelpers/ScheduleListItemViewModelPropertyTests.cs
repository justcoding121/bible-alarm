#nullable enable

using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemViewModelPropertyTests
{
    private sealed class FakeScheduleStateService(bool succeed, Exception? throwOnUpdate = null) : IScheduleStateService
    {
        public List<(int Id, bool Enabled)> Updates { get; } = [];

        public void Dispose()
        {
        }

        public Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled)
        {
            Updates.Add((scheduleId, isEnabled));
            if (throwOnUpdate is not null)
            {
                throw throwOnUpdate;
            }

            return Task.FromResult(succeed);
        }
    }

    [Fact]
    public void GetPropertiesFromSchedule_returns_defaults_when_schedule_null()
    {
        var tuple = ScheduleListItemViewModel.GetPropertiesFromSchedule(null);

        Assert.False(tuple.isEnabled);
        Assert.Equal(string.Empty, tuple.name);
        Assert.Equal(string.Empty, tuple.timeText);
        Assert.Equal("00", tuple.hour);
        Assert.Equal("00", tuple.minute);
        Assert.Equal("AM", tuple.meridianText);
        Assert.Equal((WeekDays)0, tuple.daysOfWeek);
        Assert.False(tuple.musicEnabled);
    }

    [Fact]
    public void GetPropertiesFromSchedule_maps_alarm_schedule_fields()
    {
        var schedule = new AlarmSchedule
        {
            Name = "Morning",
            IsEnabled = true,
            Hour = 14,
            Minute = 5,
            DaysOfWeek = WeekDays.Monday | WeekDays.Tuesday,
            MusicEnabled = true,
        };

        var tuple = ScheduleListItemViewModel.GetPropertiesFromSchedule(schedule);

        Assert.True(tuple.isEnabled);
        Assert.Equal("Morning", tuple.name);
        Assert.Equal(schedule.TimeText, tuple.timeText);
        Assert.Equal("02", tuple.hour);
        Assert.Equal("05", tuple.minute);
        Assert.Equal("PM", tuple.meridianText);
        Assert.Equal(schedule.DaysOfWeek, tuple.daysOfWeek);
        Assert.True(tuple.musicEnabled);
    }

    [Fact]
    public async Task HandleIsEnabledChanged_calls_properties_when_update_succeeds()
    {
        var state = new FakeScheduleStateService(succeed: true);

        var thisNotify = 0;
        var propsNotify = 0;
        var revertCalls = 0;

        await ScheduleListItemViewModel.HandleIsEnabledChangedAsync(
            TestLogging.CreateLogger(),
            state,
            7,
            true,
            notifyThisPropertyChanged: () => thisNotify++,
            notifyPropertiesChanged: () => propsNotify++,
            revertChange: _ =>
            {
                revertCalls++;
                return Task.CompletedTask;
            });

        Assert.Equal(1, thisNotify);
        Assert.Equal(1, propsNotify);
        Assert.Equal(0, revertCalls);
        Assert.Equal((7, true), Assert.Single(state.Updates));
    }

    [Fact]
    public async Task HandleIsEnabledChanged_reverts_when_update_fails()
    {
        var state = new FakeScheduleStateService(succeed: false);

        var revertArg = false;
        await ScheduleListItemViewModel.HandleIsEnabledChangedAsync(
            TestLogging.CreateLogger(),
            state,
            3,
            false,
            notifyThisPropertyChanged: () => { },
            notifyPropertiesChanged: () => { },
            revertChange: async v =>
            {
                revertArg = v;
                await Task.CompletedTask;
            });

        Assert.False(revertArg);
    }

    [Fact]
    public async Task HandleIsEnabledChanged_reverts_when_update_throws()
    {
        var state = new FakeScheduleStateService(succeed: true, throwOnUpdate: new InvalidOperationException("db"));

        var revertArg = false;
        await ScheduleListItemViewModel.HandleIsEnabledChangedAsync(
            TestLogging.CreateLogger(),
            state,
            5,
            true,
            notifyThisPropertyChanged: () => { },
            notifyPropertiesChanged: () => { },
            revertChange: v =>
            {
                revertArg = v;
                return Task.CompletedTask;
            });

        Assert.True(revertArg);
    }

    [Fact]
    public async Task HandleIsEnabledChanged_swallows_revert_failure_after_update_throws()
    {
        var state = new FakeScheduleStateService(succeed: true, throwOnUpdate: new InvalidOperationException("db"));

        await ScheduleListItemViewModel.HandleIsEnabledChangedAsync(
            TestLogging.CreateLogger(),
            state,
            5,
            true,
            notifyThisPropertyChanged: () => { },
            notifyPropertiesChanged: () => { },
            revertChange: _ => throw new InvalidOperationException("revert"));
    }
}
