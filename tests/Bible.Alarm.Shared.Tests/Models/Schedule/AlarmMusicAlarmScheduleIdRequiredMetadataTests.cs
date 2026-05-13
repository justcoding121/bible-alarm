#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmMusicAlarmScheduleIdRequiredMetadataTests
{
    [Fact]
    public void AlarmScheduleId_is_marked_required()
    {
        var p = typeof(AlarmMusic).GetProperty(nameof(AlarmMusic.AlarmScheduleId))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
