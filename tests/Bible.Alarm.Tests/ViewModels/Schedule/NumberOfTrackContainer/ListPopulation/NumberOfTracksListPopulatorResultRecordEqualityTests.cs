#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer.ListPopulation;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTracksListPopulatorResultRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var a = new NumberOfTracksListPopulatorResult(null!, null);
        var b = new NumberOfTracksListPopulatorResult(a.List, a.SelectedItem);
        Assert.Equal(a, b);
    }
}
