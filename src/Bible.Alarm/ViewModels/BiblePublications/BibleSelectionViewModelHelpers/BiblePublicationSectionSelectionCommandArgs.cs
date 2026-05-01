#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

public sealed record SectionSelectionSelectors(
    Func<LanguageListViewItemModel?> GetCurrentLanguage,
    Func<ObservableCollection<PublicationListViewItemModel>> GetPublications,
    Func<Dictionary<string, PublicationListViewItemModel>> GetPublicationVMsMapping,
    Func<BiblePublicationSchedule?> GetCurrent);

public sealed record SectionSelectionUiBindings(
    Action<bool> SetShowProgress,
    Action<double> SetProgressPercent,
    Action<string> SetProgressText,
    Action<bool> SetIsBusy);
