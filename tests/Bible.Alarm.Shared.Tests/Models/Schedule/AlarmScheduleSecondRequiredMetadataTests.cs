#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleSecondRequiredMetadataTests
{
    [Fact]
    public void Second_is_marked_required()
    {
        var p = typeof(AlarmSchedule).GetProperty(nameof(AlarmSchedule.Second))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
