#nullable enable

using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ScheduleValidationServiceTests
{
    private sealed class RecordingToastService : IToastService
    {
        public List<string> Messages { get; } = [];

        public Task ShowMessage(string message, int seconds = 3)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ShowScheduledNotification(Bible.Alarm.Shared.Models.Schedule.AlarmSchedule schedule, int seconds = 3) =>
            Task.CompletedTask;

        public Task Clear() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task ValidateDaysOfWeekAsync_true_when_any_day_selected()
    {
        var toast = new RecordingToastService();
        var sut = new ScheduleValidationService(TestLogging.CreateLogger(), toast);

        Assert.True(await sut.ValidateDaysOfWeekAsync(WeekDays.Monday));

        Assert.Empty(toast.Messages);
    }

    [Fact]
    public async Task ValidateDaysOfWeekAsync_false_and_toast_when_none_selected()
    {
        var toast = new RecordingToastService();
        var sut = new ScheduleValidationService(TestLogging.CreateLogger(), toast);

        Assert.False(await sut.ValidateDaysOfWeekAsync(0));

        Assert.Equal(AppConstants.ToastMessages.SelectAtLeastOneDay, Assert.Single(toast.Messages));
    }
}
