#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class ScheduleViewModelManagerTests
{
    [Fact]
    public void UpdateScheduleViewModels_skips_non_positive_schedule_ids()
    {
        var sut = new ScheduleViewModelManager(
            TestLogging.CreateLogger(),
            new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider(),
            _ => { });
        var items = new ObservableHashSet<ScheduleStateItem>
        {
            new() { Id = 0, DaysOfWeek = WeekDays.Monday },
            new() { Id = -3, DaysOfWeek = WeekDays.Monday },
        };

        sut.UpdateScheduleViewModels(items);

        Assert.Empty(sut.ScheduleViewModels);
    }

    [Fact]
    public void DisposeAll_clears_schedule_view_model_dictionary()
    {
        var sut = new ScheduleViewModelManager(
            TestLogging.CreateLogger(),
            new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider(),
            _ => { });

        sut.DisposeAll();

        Assert.Empty(sut.ScheduleViewModels);
    }
}
