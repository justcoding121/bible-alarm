#nullable enable

using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;

namespace Bible.Alarm.Tests;

public sealed class MusicStateChangeHandlerCollaboratorsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new MusicStateChangeHandlerCollaborators(null!, null!, null!);
        var b = new MusicStateChangeHandlerCollaborators(
            a.StateTracker,
            a.PropertyNotifier,
            a.DisplayTextProvider);

        Assert.Equal(a, b);
    }
}
