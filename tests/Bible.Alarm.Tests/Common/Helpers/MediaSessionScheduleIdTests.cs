#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class MediaSessionScheduleIdTests
{
    [Fact]
    public void Resolve_uses_new_schedule_id_when_the_shown_schedule_changes()
    {
        Assert.Equal("42", MediaSessionScheduleId.Resolve(existingMediaId: "7", scheduleId: 42));
    }

    [Fact]
    public void Resolve_keeps_existing_media_id_when_no_new_schedule_is_supplied()
    {
        Assert.Equal("7", MediaSessionScheduleId.Resolve(existingMediaId: "7", scheduleId: null));
        Assert.Equal("7", MediaSessionScheduleId.Resolve(existingMediaId: "7", scheduleId: 0));
    }

    [Fact]
    public void IsScheduleChange_is_true_only_when_the_id_differs()
    {
        Assert.True(MediaSessionScheduleId.IsScheduleChange("7", 42));
        Assert.False(MediaSessionScheduleId.IsScheduleChange("42", 42));
        Assert.False(MediaSessionScheduleId.IsScheduleChange("7", null));
    }
}
