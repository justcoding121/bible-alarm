#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationScheduledTimeRequiredMetadataTests
{
    [Fact]
    public void ScheduledTime_is_marked_required()
    {
        var p = typeof(AlarmNotification).GetProperty(nameof(AlarmNotification.ScheduledTime))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
