#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleSnoozeMinutesRequiredMetadataTests
{
    [Fact]
    public void SnoozeMinutes_is_marked_required()
    {
        var p = typeof(AlarmSchedule).GetProperty(nameof(AlarmSchedule.SnoozeMinutes))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
