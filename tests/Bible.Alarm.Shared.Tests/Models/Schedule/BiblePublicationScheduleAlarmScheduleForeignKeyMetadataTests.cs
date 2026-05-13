#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationScheduleAlarmScheduleForeignKeyMetadataTests
{
    [Fact]
    public void AlarmScheduleId_foreign_key_targets_schedule_navigation()
    {
        var fk = typeof(BiblePublicationSchedule).GetProperty(nameof(BiblePublicationSchedule.AlarmScheduleId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(BiblePublicationSchedule.AlarmSchedule), fk.Name);
    }
}
