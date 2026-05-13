#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleHourRequiredMetadataTests
{
    [Fact]
    public void Hour_is_marked_required()
    {
        var p = typeof(AlarmSchedule).GetProperty(nameof(AlarmSchedule.Hour))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
