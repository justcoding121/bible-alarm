#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmMusicAlarmScheduleNavigationRequiredMetadataTests
{
    [Fact]
    public void AlarmSchedule_navigation_is_marked_required()
    {
        var p = typeof(AlarmMusic).GetProperty(nameof(AlarmMusic.AlarmSchedule))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
