#nullable enable

using Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicSectionSelectionStateChangeHandlerCallbacksRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new MusicSectionSelectionStateChangeHandlerCallbacks(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new MusicSectionSelectionStateChangeHandlerCallbacks(
            a.SetLastPublicationCode,
            a.SetLastSectionCode,
            a.GetInitComplete,
            a.SetIsBusy,
            a.Initialize,
            a.SetSelectedSection);

        Assert.Equal(a, b);
    }
}
