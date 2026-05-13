#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleNameRequiredMetadataTests
{
    [Fact]
    public void Name_is_marked_required()
    {
        var p = typeof(AlarmSchedule).GetProperty(nameof(AlarmSchedule.Name))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
