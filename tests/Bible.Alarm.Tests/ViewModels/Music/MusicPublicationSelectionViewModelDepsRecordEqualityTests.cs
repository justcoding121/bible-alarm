#nullable enable

using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionViewModelDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new MusicPublicationSelectionViewModelDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new MusicPublicationSelectionViewModelDeps(
            a.MediaService,
            a.ScopeFactory,
            a.ApplicationState,
            a.Dispatcher,
            a.NavigationService,
            a.ServiceProvider);

        Assert.Equal(a, b);
    }
}
