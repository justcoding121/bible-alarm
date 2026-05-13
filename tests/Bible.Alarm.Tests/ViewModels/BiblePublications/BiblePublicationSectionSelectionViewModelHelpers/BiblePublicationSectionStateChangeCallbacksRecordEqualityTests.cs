#nullable enable

using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionStateChangeCallbacksRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new BiblePublicationSectionStateChangeCallbacks(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new BiblePublicationSectionStateChangeCallbacks(
            a.SetCurrent,
            a.SetLastCurrent,
            a.GetInitComplete,
            a.SetIsBusy,
            a.Initialize,
            a.SetSelectedSection);

        Assert.Equal(a, b);
    }
}
