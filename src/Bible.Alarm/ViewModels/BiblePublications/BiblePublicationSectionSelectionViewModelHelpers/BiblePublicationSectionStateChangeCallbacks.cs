#nullable enable

using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;

public sealed record BiblePublicationSectionStateChangeCallbacks(
    Action<BiblePublicationSchedule> SetCurrent,
    Action<BiblePublicationSchedule> SetLastCurrent,
    Func<bool> GetInitComplete,
    Action<bool> SetIsBusy,
    Action<string, string> Initialize,
    Action SetSelectedSection);
