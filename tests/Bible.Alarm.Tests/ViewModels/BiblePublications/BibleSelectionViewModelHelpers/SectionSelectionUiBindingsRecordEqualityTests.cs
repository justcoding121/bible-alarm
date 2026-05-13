#nullable enable

using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class SectionSelectionUiBindingsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new SectionSelectionUiBindings(null!, null!, null!, null!);
        var b = new SectionSelectionUiBindings(
            a.SetShowProgress,
            a.SetProgressPercent,
            a.SetProgressText,
            a.SetIsBusy);

        Assert.Equal(a, b);
    }
}
