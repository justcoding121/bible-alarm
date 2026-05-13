#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackStopRequestRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_field_values_are_equal()
    {
        var a = new PlaybackStopRequest(
            ScheduleIdToSave: null,
            TrackMetadataToMark: null,
            SkipMarkAsPlayed: false,
            SkipSaveLastPlayed: false,
            PreparationCancellationTokenSource: null,
            ResetState: null!,
            StopProgressTimer: null!);

        var b = new PlaybackStopRequest(
            a.ScheduleIdToSave,
            a.TrackMetadataToMark,
            a.SkipMarkAsPlayed,
            a.SkipSaveLastPlayed,
            a.PreparationCancellationTokenSource,
            a.ResetState,
            a.StopProgressTimer);

        Assert.Equal(a, b);
    }
}
