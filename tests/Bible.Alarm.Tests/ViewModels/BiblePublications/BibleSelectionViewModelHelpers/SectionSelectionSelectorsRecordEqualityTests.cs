#nullable enable

using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class SectionSelectionSelectorsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new SectionSelectionSelectors(null!, null!, null!, null!);
        var b = new SectionSelectionSelectors(
            a.GetCurrentLanguage,
            a.GetPublications,
            a.GetPublicationVMsMapping,
            a.GetCurrent);

        Assert.Equal(a, b);
    }
}
