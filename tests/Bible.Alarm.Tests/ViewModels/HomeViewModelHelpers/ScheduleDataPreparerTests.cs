#nullable enable

using System.Linq;
using AutoMapper;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleDataPreparerTests
{
    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            cfg => cfg.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleStateItem Schedule(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = true,
            Hour = 7,
            Minute = 15,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 9,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
        };

    [Fact]
    public void PrepareScheduleDataOffUIThread_skips_non_positive_ids()
    {
        var mapper = CreateMapper();
        var sut = new ScheduleDataPreparer(mapper);
        var items = new ObservableHashSet<ScheduleStateItem>
        {
            new() { Id = 0, Name = "draft" },
            new() { Id = -1, Name = "bad" },
            Schedule(5, "keep"),
        };

        var (dataMap, stateMap) = sut.PrepareScheduleDataOffUIThread(items);

        Assert.Single(dataMap);
        Assert.Single(stateMap);
        Assert.True(dataMap.ContainsKey(5));
        Assert.True(stateMap.ContainsKey(5));
        Assert.Equal("keep", stateMap[5].Name);
        Assert.Equal("keep", dataMap[5].Name);
    }

    [Fact]
    public void PrepareScheduleDataOffUIThread_maps_each_saved_schedule()
    {
        var mapper = CreateMapper();
        var sut = new ScheduleDataPreparer(mapper);
        var items = new ObservableHashSet<ScheduleStateItem>
        {
            Schedule(1, "One"),
            Schedule(2, "Two"),
        };

        var (dataMap, stateMap) = sut.PrepareScheduleDataOffUIThread(items);

        Assert.Equal(2, dataMap.Count);
        Assert.Equal(2, stateMap.Count);
        Assert.Equal(1, dataMap[1].Id);
        Assert.Equal(2, dataMap[2].Id);
        Assert.Same(items.First(i => i.Id == 1), stateMap[1]);
    }
}
