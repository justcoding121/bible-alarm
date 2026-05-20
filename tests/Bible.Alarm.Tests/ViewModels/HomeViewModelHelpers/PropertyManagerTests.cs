#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class PropertyManagerTests
{
    [Fact]
    public void Schedules_getter_returns_assigned_collection()
    {
        var set = new ObservableHashSet<ScheduleListItemViewModel>();
        var sut = new PropertyManager { Schedules = set };

        Assert.Same(set, sut.Schedules);
    }

    [Fact]
    public void Schedules_setter_raises_when_reference_changes()
    {
        var sut = new PropertyManager();
        var raised = 0;
        sut.SchedulesChanged += () => raised++;

        sut.Schedules = new ObservableHashSet<ScheduleListItemViewModel>();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void IsBusy_false_marks_loaded_and_raises_events()
    {
        var sut = new PropertyManager();
        var busyArgs = new List<bool>();
        var loadedArgs = new List<bool>();
        sut.IsBusyChanged += busyArgs.Add;
        sut.LoadedChanged += loadedArgs.Add;

        sut.IsBusy = false;

        Assert.False(busyArgs.Single());
        Assert.True(loadedArgs.Single());
        Assert.False(sut.IsBusy);
        Assert.True(sut.Loaded);
    }

    [Fact]
    public void IsAddBusy_toggle_raises()
    {
        var sut = new PropertyManager();
        var args = new List<bool>();
        sut.IsAddBusyChanged += args.Add;

        sut.IsAddBusy = true;

        Assert.True(args.Single());
        Assert.True(sut.IsAddBusy);
    }

    [Fact]
    public void Schedules_setter_does_not_raise_when_reference_unchanged()
    {
        var set = new ObservableHashSet<ScheduleListItemViewModel>();
        var sut = new PropertyManager { Schedules = set };
        var raised = 0;
        sut.SchedulesChanged += () => raised++;

        sut.Schedules = set;

        Assert.Equal(0, raised);
    }

    [Fact]
    public void IsBusy_setter_does_not_raise_when_value_unchanged()
    {
        var sut = new PropertyManager();
        var busyRaised = 0;
        var loadedRaised = 0;
        sut.IsBusyChanged += _ => busyRaised++;
        sut.LoadedChanged += _ => loadedRaised++;

        sut.IsBusy = true;

        Assert.Equal(0, busyRaised);
        Assert.Equal(0, loadedRaised);
    }

    [Fact]
    public void Loaded_setter_raises_when_value_changes()
    {
        var sut = new PropertyManager();
        var args = new List<bool>();
        sut.LoadedChanged += args.Add;

        sut.Loaded = true;

        Assert.True(args.Single());
        Assert.True(sut.Loaded);
    }

    [Fact]
    public void IsAddBusy_setter_does_not_raise_when_value_unchanged()
    {
        var sut = new PropertyManager();
        var raised = 0;
        sut.IsAddBusyChanged += _ => raised++;

        sut.IsAddBusy = false;

        Assert.Equal(0, raised);
    }
}
