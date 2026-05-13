#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationAnnotationsTests
{
    [Fact]
    public void Indexes_cover_schedule_time_and_sent_fired_composite()
    {
        var indexes = typeof(AlarmNotification).GetCustomAttributes(typeof(IndexAttribute), inherit: false)
            .Cast<IndexAttribute>()
            .ToList();

        Assert.Equal(3, indexes.Count);

        Assert.Contains(indexes, i =>
            i.PropertyNames.Count == 1 && i.PropertyNames[0] == nameof(AlarmNotification.AlarmScheduleId));

        Assert.Contains(indexes, i =>
            i.PropertyNames.Count == 1 && i.PropertyNames[0] == nameof(AlarmNotification.ScheduledTime));

        Assert.Contains(indexes, i =>
            i.PropertyNames.SequenceEqual(new[]
            {
                nameof(AlarmNotification.Sent),
                nameof(AlarmNotification.Fired),
            }));
    }
}
