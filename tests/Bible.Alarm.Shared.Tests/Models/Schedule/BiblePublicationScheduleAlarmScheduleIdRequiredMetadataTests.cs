#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationScheduleAlarmScheduleIdRequiredMetadataTests
{
    [Fact]
    public void AlarmScheduleId_is_marked_required()
    {
        var p = typeof(BiblePublicationSchedule).GetProperty(nameof(BiblePublicationSchedule.AlarmScheduleId))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
