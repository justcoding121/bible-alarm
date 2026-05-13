#nullable enable

using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class TrackSelectionProgressBindingsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new TrackSelectionProgressBindings(null!, null!, null!);
        var b = new TrackSelectionProgressBindings(
            a.SetShowProgress,
            a.SetProgressPercent,
            a.SetProgressText);

        Assert.Equal(a, b);
    }
}
