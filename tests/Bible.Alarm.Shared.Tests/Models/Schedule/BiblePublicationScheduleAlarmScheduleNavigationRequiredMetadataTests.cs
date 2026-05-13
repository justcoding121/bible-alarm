#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationScheduleAlarmScheduleNavigationRequiredMetadataTests
{
    [Fact]
    public void AlarmSchedule_navigation_is_marked_required()
    {
        var p = typeof(BiblePublicationSchedule).GetProperty(nameof(BiblePublicationSchedule.AlarmSchedule))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
