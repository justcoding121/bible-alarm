#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmMusicAlarmScheduleForeignKeyMetadataTests
{
    [Fact]
    public void AlarmScheduleId_foreign_key_targets_schedule_navigation()
    {
        var fk = typeof(AlarmMusic).GetProperty(nameof(AlarmMusic.AlarmScheduleId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(AlarmMusic.AlarmSchedule), fk.Name);
    }
}
