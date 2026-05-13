#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleLatestAlarmNotificationIdRequiredMetadataTests
{
    [Fact]
    public void LatestAlarmNotificationId_is_marked_required()
    {
        var p = typeof(AlarmSchedule).GetProperty(nameof(AlarmSchedule.LatestAlarmNotificationId))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
