#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleNotificationEnabledRequiredMetadataTests
{
    [Fact]
    public void NotificationEnabled_is_marked_required()
    {
        var p = typeof(AlarmSchedule).GetProperty(nameof(AlarmSchedule.NotificationEnabled))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
