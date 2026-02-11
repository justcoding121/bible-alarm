#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer.ListPopulation;

/// <summary>
/// Result of populating the number of tracks list.
/// </summary>
public sealed record NumberOfTracksListPopulatorResult(
    ObservableCollection<NumberOfTracksListViewItemModel> List,
    NumberOfTracksListViewItemModel? SelectedItem);
