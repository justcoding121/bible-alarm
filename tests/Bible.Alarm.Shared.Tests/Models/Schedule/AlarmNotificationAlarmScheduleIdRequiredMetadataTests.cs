#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationAlarmScheduleIdRequiredMetadataTests
{
    [Fact]
    public void AlarmScheduleId_is_marked_required()
    {
        var p = typeof(AlarmNotification).GetProperty(nameof(AlarmNotification.AlarmScheduleId))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
