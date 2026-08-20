#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainerViewModelHelpers.ListPopulation;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTracksListPopulatorResultTests
{
    [Fact]
    public void Record_exposes_list_and_selected_item()
    {
        var list = new ObservableCollection<NumberOfTracksListViewItemModel>();
        var selected = new NumberOfTracksListViewItemModel(3, "track", "tracks");

        var sut = new NumberOfTracksListPopulatorResult(list, selected);

        Assert.Same(list, sut.List);
        Assert.Same(selected, sut.SelectedItem);
    }

    [Fact]
    public void Record_allows_null_selected_item()
    {
        var list = new ObservableCollection<NumberOfTracksListViewItemModel>();
        var sut = new NumberOfTracksListPopulatorResult(list, SelectedItem: null);

        Assert.Null(sut.SelectedItem);
    }
}
