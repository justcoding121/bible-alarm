#nullable enable

using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

public sealed class MusicSectionSelectionRefreshContext
{
    public required Func<bool> IsDisposed { get; init; }
    public required Func<bool> IsSelectingSection { get; init; }
    public required Action<bool> SetIsBusy { get; init; }
    public required Action<bool> SetCanCancelFetch { get; init; }
    public required Action<bool> SetShowProgress { get; init; }
    public required Action<string> SetProgressText { get; init; }
    public required Action<double> SetProgressPercent { get; init; }
    public required Action<bool> SetScreenOn { get; init; }
    public required Func<string, IFetchProgress?, Task> PopulateSections { get; init; }
    public required Action SetSelectedSection { get; init; }
}
