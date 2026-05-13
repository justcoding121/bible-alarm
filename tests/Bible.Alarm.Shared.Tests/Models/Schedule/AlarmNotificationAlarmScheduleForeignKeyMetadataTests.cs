#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationAlarmScheduleForeignKeyMetadataTests
{
    [Fact]
    public void AlarmScheduleId_foreign_key_targets_schedule_navigation()
    {
        var fk = typeof(AlarmNotification).GetProperty(nameof(AlarmNotification.AlarmScheduleId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(AlarmNotification.AlarmSchedule), fk.Name);
    }
}
