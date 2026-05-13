#nullable enable

using Bible.Alarm.ViewModels.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionViewModelDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new BiblePublicationSelectionViewModelDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new BiblePublicationSelectionViewModelDeps(
            a.MediaService,
            a.ScopeFactory,
            a.ApplicationState,
            a.Dispatcher,
            a.NavigationService,
            a.ServiceProvider);

        Assert.Equal(a, b);
    }
}
