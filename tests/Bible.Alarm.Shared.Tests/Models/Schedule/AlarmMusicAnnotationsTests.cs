#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmMusicAnnotationsTests
{
    [Fact]
    public void Type_is_marked_with_unique_index_on_alarm_schedule_key()
    {
        var indexes = typeof(AlarmMusic).GetCustomAttributes(typeof(IndexAttribute), inherit: false).Cast<IndexAttribute>();
        var uniqueOnSchedule = Assert.Single(indexes, a =>
            a.IsUnique && a.PropertyNames.Count == 1 && a.PropertyNames[0] == nameof(AlarmMusic.AlarmScheduleId));
        Assert.True(uniqueOnSchedule.IsUnique);
    }

    [Fact]
    public void Type_has_non_unique_composite_index_on_publication_and_language_codes()
    {
        var indexes = typeof(AlarmMusic).GetCustomAttributes(typeof(IndexAttribute), inherit: false)
            .Cast<IndexAttribute>()
            .ToList();

        _ = Assert.Single(indexes, a =>
            !a.IsUnique &&
            a.PropertyNames.SequenceEqual(new[] { nameof(AlarmMusic.PublicationCode), nameof(AlarmMusic.LanguageCode) }));
    }
}
