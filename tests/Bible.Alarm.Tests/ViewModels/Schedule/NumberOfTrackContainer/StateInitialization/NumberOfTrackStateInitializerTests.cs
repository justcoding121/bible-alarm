#nullable enable

using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer.StateInitialization;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTrackStateInitializerTests
{
    [Fact]
    public void TryInitialize_returns_null_without_schedule()
    {
        var logger = TestLogging.CreateLogger();

        var result = NumberOfTrackStateInitializer.TryInitialize(null, () => true, logger);

        Assert.Null(result);
    }

    [Fact]
    public void TryInitialize_maps_schedule_fields_for_windows_tfm()
    {
        var logger = TestLogging.CreateLogger();
        var schedule = new ScheduleStateItem
        {
            Id = 15,
            NotificationEnabled = true,
            AlwaysPlayFromStart = true,
            NumberOfTracksToPlay = 0,
            BiblePublicationCategoryName = "Audio Bible",
        };

        var result = NumberOfTrackStateInitializer.TryInitialize(schedule, () => false, logger);

        Assert.NotNull(result);
        Assert.Equal(15, result.ScheduleId);
        Assert.True(result.NotificationEnabled);
        Assert.True(result.AlwaysPlayFromStart);
        Assert.True(result.PlayIndefinitely);
        Assert.Equal("Audio Bible", result.LastCategoryName);
    }

    [Fact]
    public void PlayIndefinitely_false_when_positive_track_count()
    {
        var logger = TestLogging.CreateLogger();
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            NotificationEnabled = false,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 3,
        };

        var result = NumberOfTrackStateInitializer.TryInitialize(schedule, () => true, logger);

        Assert.NotNull(result);
        Assert.False(result.PlayIndefinitely);
    }
}
