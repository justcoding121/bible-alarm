#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationCancellationRequestedRequiredMetadataTests
{
    [Fact]
    public void CancellationRequested_is_marked_required()
    {
        var p = typeof(AlarmNotification).GetProperty(nameof(AlarmNotification.CancellationRequested))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
