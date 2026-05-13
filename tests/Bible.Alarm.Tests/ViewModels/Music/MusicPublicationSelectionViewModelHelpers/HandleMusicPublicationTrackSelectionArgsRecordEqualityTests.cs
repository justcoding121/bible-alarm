#nullable enable

using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class HandleMusicPublicationTrackSelectionArgsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var progress = new TrackSelectionProgressBindings(null!, null!, null!);
        var a = new HandleMusicPublicationTrackSelectionArgs(
            null!,
            null,
            null!,
            null,
            progress);

        var b = new HandleMusicPublicationTrackSelectionArgs(
            a.SongPublication,
            a.CurrentLanguage,
            a.DataProvider,
            a.Current,
            a.Progress);

        Assert.Equal(a, b);
    }
}
