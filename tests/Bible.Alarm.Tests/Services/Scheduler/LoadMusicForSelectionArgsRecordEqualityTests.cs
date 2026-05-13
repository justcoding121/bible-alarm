#nullable enable

using Bible.Alarm.Services.Scheduler;

namespace Bible.Alarm.Tests;

public sealed class LoadMusicForSelectionArgsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_field_values_are_equal()
    {
        var a = new LoadMusicForSelectionArgs(
            ScheduleId: 0,
            IsNewSchedule: false,
            CurrentMusic: null,
            PublicationCode: null,
            LanguageCode: null,
            TrackCode: null,
            Repeat: null);

        var b = new LoadMusicForSelectionArgs(
            a.ScheduleId,
            a.IsNewSchedule,
            a.CurrentMusic,
            a.PublicationCode,
            a.LanguageCode,
            a.TrackCode,
            a.Repeat);

        Assert.Equal(a, b);
    }
}
