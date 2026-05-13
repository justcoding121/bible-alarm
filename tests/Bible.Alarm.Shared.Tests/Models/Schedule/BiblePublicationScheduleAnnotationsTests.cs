#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationScheduleAnnotationsTests
{
    [Fact]
    public void Has_unique_index_on_alarm_schedule_and_non_unique_composite_on_publication_language()
    {
        var indexes = typeof(BiblePublicationSchedule).GetCustomAttributes(typeof(IndexAttribute), inherit: false)
            .Cast<IndexAttribute>()
            .ToList();

        _ = Assert.Single(indexes, a =>
            a.IsUnique &&
            a.PropertyNames.SequenceEqual(new[] { nameof(BiblePublicationSchedule.AlarmScheduleId) }));

        _ = Assert.Single(indexes, a =>
            !a.IsUnique &&
            a.PropertyNames.SequenceEqual(new[]
            {
                nameof(BiblePublicationSchedule.PublicationCode),
                nameof(BiblePublicationSchedule.LanguageCode),
            }));
    }
}
