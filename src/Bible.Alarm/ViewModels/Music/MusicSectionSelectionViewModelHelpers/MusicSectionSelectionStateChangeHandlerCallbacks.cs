#nullable enable

namespace Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

public sealed record MusicSectionSelectionStateChangeHandlerCallbacks(
    Action<string?> SetLastPublicationCode,
    Action<string?> SetLastSectionCode,
    Func<bool> GetInitComplete,
    Action<bool> SetIsBusy,
    Action<string> Initialize,
    Action SetSelectedSection);
