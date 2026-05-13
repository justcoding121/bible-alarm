#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationScheduleDataAnnotationsTests
{
    [Fact]
    public void Type_is_serializable_and_maps_to_BiblePublicationSchedules()
    {
        Assert.NotNull(typeof(BiblePublicationSchedule).GetCustomAttributes(typeof(SerializableAttribute), inherit: false).FirstOrDefault());

        var table = typeof(BiblePublicationSchedule).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("BiblePublicationSchedules", table.Name);
    }

    [Fact]
    public void Codes_respect_expected_max_lengths()
    {
        Assert.Equal(10, MaxLengthOf(nameof(BiblePublicationSchedule.LanguageCode)));
        Assert.Equal(50, MaxLengthOf(nameof(BiblePublicationSchedule.PublicationCode)));
        Assert.Equal(50, MaxLengthOf(nameof(BiblePublicationSchedule.SectionCode)));
        Assert.Equal(50, MaxLengthOf(nameof(BiblePublicationSchedule.TrackCode)));
    }

    private static int MaxLengthOf(string propertyName) =>
        typeof(BiblePublicationSchedule).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single()
            .Length;
}
