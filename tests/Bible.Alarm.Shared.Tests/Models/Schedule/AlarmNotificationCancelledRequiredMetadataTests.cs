#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationCancelledRequiredMetadataTests
{
    [Fact]
    public void Cancelled_is_marked_required()
    {
        var p = typeof(AlarmNotification).GetProperty(nameof(AlarmNotification.Cancelled))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
