#nullable enable

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleAnnotationsTests
{
    [Fact]
    public void Type_is_serializable_mapped_to_AlarmSchedules_with_expected_indexes()
    {
        Assert.NotNull(typeof(AlarmSchedule).GetCustomAttributes(typeof(SerializableAttribute), inherit: false).FirstOrDefault());

        var table = typeof(AlarmSchedule).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("AlarmSchedules", table.Name);

        var indexes = typeof(AlarmSchedule).GetCustomAttributes(typeof(IndexAttribute), inherit: false)
            .Cast<IndexAttribute>()
            .ToList();

        _ = Assert.Single(indexes, a =>
            !a.IsUnique && a.PropertyNames.SequenceEqual(new[] { nameof(AlarmSchedule.IsEnabled) }));

        _ = Assert.Single(indexes, a =>
            !a.IsUnique &&
            a.PropertyNames.SequenceEqual(new[] { nameof(AlarmSchedule.Hour), nameof(AlarmSchedule.Minute) }));
    }
}
