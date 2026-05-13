#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleIsEnabledRequiredMetadataTests
{
    [Fact]
    public void IsEnabled_is_marked_required()
    {
        var p = typeof(AlarmSchedule).GetProperty(nameof(AlarmSchedule.IsEnabled))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
