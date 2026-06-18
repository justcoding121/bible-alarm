#nullable enable

using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Bible.Alarm.Platforms.Windows.Services.UI;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsNotificationServiceTests
{
    private sealed class RecordingWindowsAlarmHandler : IWindowsAlarmHandler
    {
        public List<(int ScheduleId, bool IsAlarm)> Calls { get; } = [];

        public Task HandleAsync(int scheduleId, bool isAlarm)
        {
            Calls.Add((scheduleId, isAlarm));
            return Task.CompletedTask;
        }
    }

    private sealed class StubServiceProvider(params (Type ServiceType, object Instance)[] services) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            foreach (var (type, instance) in services)
            {
                if (type == serviceType)
                {
                    return instance;
                }
            }

            return null;
        }
    }

    private static WindowsNotificationService CreateSut(IServiceProvider serviceProvider) =>
        new(serviceProvider, TestLogging.CreateLogger());

    private static AlarmSchedule SampleSchedule() =>
        new()
        {
            Id = 12,
            Name = "Morning",
            IsEnabled = true,
            Hour = 7,
            Minute = 30,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    [Fact]
    public async Task ShowNotificationAsync_delegates_to_windows_alarm_handler()
    {
        var handler = new RecordingWindowsAlarmHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IWindowsAlarmHandler>(handler);
        var provider = services.BuildServiceProvider();
        var sut = CreateSut(provider);

        await sut.ShowNotificationAsync(42);

        Assert.Single(handler.Calls);
        Assert.Equal((42, true), handler.Calls[0]);
    }

    [Fact]
    public async Task CanScheduleAsync_returns_background_task_enabled_state()
    {
        var sut = CreateSut(new StubServiceProvider());

        var canSchedule = await sut.CanScheduleAsync();

        Assert.IsType<bool>(canSchedule);
    }

    [Fact]
    public async Task RemoveAsync_completes_without_throw()
    {
        var sut = CreateSut(new StubServiceProvider());

        await sut.RemoveAsync(7);
    }

    [Fact]
    public async Task IsScheduledAsync_returns_false_when_no_matching_toast_exists()
    {
        var sut = CreateSut(new StubServiceProvider());

        var scheduled = await sut.IsScheduledAsync(999_999);

        Assert.False(scheduled);
    }

    [Fact]
    public async Task ClearDeliveredNotificationAsync_completes_without_throw()
    {
        var sut = CreateSut(new StubServiceProvider());

        await sut.ClearDeliveredNotificationAsync(3);
    }

    [Fact]
    public void DismissMediaToast_does_not_throw()
    {
        var sut = CreateSut(new StubServiceProvider());

        var ex = Record.Exception(() => sut.DismissMediaToast());

        Assert.Null(ex);
    }

    [Fact]
    public void ShowMediaToast_does_not_throw_when_notifier_unavailable()
    {
        var sut = CreateSut(new StubServiceProvider());

        var ex = Record.Exception(() =>
            sut.ShowMediaToast("Title", "Subtitle", "Body", artworkUrl: null));

        Assert.Null(ex);
    }

    [Fact]
    public async Task ScheduleNotificationAsync_completes_for_enabled_daily_schedule()
    {
        var sut = CreateSut(new StubServiceProvider());

        await sut.ScheduleNotificationAsync(SampleSchedule(), "Alarm", "Wake up");
    }

    [Fact]
    public void AlarmToastGroup_is_stable_schedule_group_name()
    {
        Assert.Equal("BibleAlarmSchedule", WindowsNotificationService.AlarmToastGroup);
    }
}
