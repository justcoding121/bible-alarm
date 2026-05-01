#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

public sealed record TrackSelectionProgressBindings(
    Action<bool> SetShowProgress,
    Action<double> SetProgressPercent,
    Action<string> SetProgressText);

public sealed record HandleMusicPublicationTrackSelectionArgs(
    PublicationListViewItemModel SongPublication,
    LanguageListViewItemModel? CurrentLanguage,
    MusicPublicationSelectionDataProvider DataProvider,
    AlarmMusic? Current,
    TrackSelectionProgressBindings Progress);

public sealed record HandleMusicLanguageSelectionUiCallbacks(
    Action<LanguageListViewItemModel?> SetCurrentLanguage,
    Action<LanguageListViewItemModel> UpdateSelectedLanguage,
    Action<bool> SetShowProgress,
    Action<double> SetProgressPercent,
    Action<string> SetProgressText,
    Action<bool> SetIsBusy);
