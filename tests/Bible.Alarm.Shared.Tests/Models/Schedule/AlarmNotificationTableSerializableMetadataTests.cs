#nullable enable

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationTableSerializableMetadataTests
{
    [Fact]
    public void Type_is_serializable_and_maps_to_AlarmNotifications()
    {
        Assert.NotNull(typeof(AlarmNotification).GetCustomAttributes(typeof(SerializableAttribute), inherit: false).FirstOrDefault());

        var table = typeof(AlarmNotification).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("AlarmNotifications", table.Name);
    }
}
