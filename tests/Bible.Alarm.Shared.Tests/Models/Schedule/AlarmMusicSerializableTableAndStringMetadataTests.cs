#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmMusicSerializableTableAndStringMetadataTests
{
    [Fact]
    public void Type_is_serializable_maps_to_AlarmMusic_and_bounds_string_columns()
    {
        Assert.NotNull(typeof(AlarmMusic).GetCustomAttributes(typeof(SerializableAttribute), inherit: false).FirstOrDefault());

        var table = typeof(AlarmMusic).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("AlarmMusic", table.Name);

        Assert.Equal(50, MaxLengthOf(nameof(AlarmMusic.PublicationCode)));
        Assert.Equal(10, MaxLengthOf(nameof(AlarmMusic.LanguageCode)));
        Assert.Equal(50, MaxLengthOf(nameof(AlarmMusic.SectionCode)));
        Assert.Equal(50, MaxLengthOf(nameof(AlarmMusic.TrackCode)));
    }

    private static int MaxLengthOf(string propertyName) =>
        typeof(AlarmMusic).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single()
            .Length;
}
