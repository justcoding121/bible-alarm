#nullable enable
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;

/// <summary>
/// Detects property changes in bible selection.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePublicationPropertyChangeDetector
{
    private readonly BiblePublicationDisplayTextProvider displayTextProvider;

    // Track last values to prevent unnecessary PropertyChanged notifications
    private string? lastLanguageDisplayText;
    private string? lastPublicationDisplayText;
    private string? lastSectionDisplayText;
    private string? lastTrackDisplayText;
    private bool? lastIsSectionVisible;

    // Track underlying property values to detect cascading changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private int? lastSectionNumber;
    private int? lastTrackNumber;

    public BiblePublicationPropertyChangeDetector(BiblePublicationDisplayTextProvider displayTextProvider)
    {
        this.displayTextProvider = displayTextProvider;
    }

    public void Initialize(
        string? languageCode,
        string? publicationCode,
        int? sectionNumber,
        int? trackNumber)
    {
        lastLanguageCode = languageCode;
        lastPublicationCode = publicationCode;
        lastSectionNumber = sectionNumber;
        lastTrackNumber = trackNumber;
        lastLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        lastPublicationDisplayText = displayTextProvider.GetPublicationDisplayText();
        lastSectionDisplayText = displayTextProvider.GetSectionDisplayText();
        lastTrackDisplayText = displayTextProvider.GetTrackDisplayText();
        lastIsSectionVisible = displayTextProvider.GetIsSectionVisible();
    }

    public PropertyChangeInfo DetectPropertyChanges(ScheduleStateItem? currentSchedule, BiblePublicationStateItem? currentBiblePublication)
    {
        var currentLanguageCode = currentSchedule?.BiblePublicationLanguageCode;
        var currentPublicationCode = currentBiblePublication?.PublicationCode ?? currentSchedule?.BiblePublicationCode;
        var currentSectionNumber = currentBiblePublication?.SectionNumber ?? currentSchedule?.BiblePublicationSectionNumber;
        var currentTrackNumber = currentBiblePublication?.TrackNumber ?? currentSchedule?.BiblePublicationTrackNumber;

        var languageCodeChanged = currentLanguageCode != lastLanguageCode;
        var publicationCodeChanged = currentPublicationCode != lastPublicationCode;
        var sectionNumberChanged = currentSectionNumber != lastSectionNumber;
        var trackNumberChanged = currentTrackNumber != lastTrackNumber;

        // Section visibility changes when publication code changes to/from a drama type
        var sectionVisibilityTypeChanged = publicationCodeChanged &&
            PublicationTypeHelper.IsDrama(currentPublicationCode) != PublicationTypeHelper.IsDrama(lastPublicationCode);

        var newLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        var newPublicationDisplayText = displayTextProvider.GetPublicationDisplayText();
        var newSectionDisplayText = displayTextProvider.GetSectionDisplayText();
        var newTrackDisplayText = displayTextProvider.GetTrackDisplayText();
        var newIsSectionVisible = displayTextProvider.GetIsSectionVisible();

        var languageDisplayChanged = newLanguageDisplayText != lastLanguageDisplayText;
        var publicationDisplayChanged = newPublicationDisplayText != lastPublicationDisplayText;
        var sectionDisplayChanged = newSectionDisplayText != lastSectionDisplayText;
        var trackDisplayChanged = newTrackDisplayText != lastTrackDisplayText;
        var isSectionVisibleChanged = newIsSectionVisible != lastIsSectionVisible;

        // Cascade logic: Language → Publication → Section → Track
        // IsSectionVisible changes when publication type changes (to/from drama)
        var notifyLanguage = languageCodeChanged;
        var notifyPublication = languageCodeChanged || publicationCodeChanged;
        var notifySection = languageCodeChanged || publicationCodeChanged || sectionNumberChanged;
        var notifyTrack = languageCodeChanged || publicationCodeChanged || sectionNumberChanged || trackNumberChanged;
        var notifyIsSectionVisible = sectionVisibilityTypeChanged || isSectionVisibleChanged;

        var displayTextOnlyChanged = (languageDisplayChanged && !languageCodeChanged) ||
                                    (publicationDisplayChanged && !languageCodeChanged && !publicationCodeChanged) ||
                                    (sectionDisplayChanged && !languageCodeChanged && !publicationCodeChanged && !sectionNumberChanged) ||
                                    (trackDisplayChanged && !languageCodeChanged && !publicationCodeChanged && !sectionNumberChanged && !trackNumberChanged);

        var cascadeChangeOccurred = languageCodeChanged || publicationCodeChanged || sectionNumberChanged || trackNumberChanged;

        var changeInfo = new PropertyChangeInfo
        {
            CurrentLanguageCode = currentLanguageCode,
            CurrentPublicationCode = currentPublicationCode,
            CurrentSectionNumber = currentSectionNumber,
            CurrentTrackNumber = currentTrackNumber,
            NewLanguageDisplayText = newLanguageDisplayText,
            NewPublicationDisplayText = newPublicationDisplayText,
            NewSectionDisplayText = newSectionDisplayText,
            NewTrackDisplayText = newTrackDisplayText,
            NewIsSectionVisible = newIsSectionVisible,
            NotifyLanguage = notifyLanguage,
            NotifyPublication = notifyPublication,
            NotifySection = notifySection,
            NotifyTrack = notifyTrack,
            NotifyIsSectionVisible = notifyIsSectionVisible,
            DisplayTextOnlyChanged = displayTextOnlyChanged,
            CascadeChangeOccurred = cascadeChangeOccurred,
            LanguageDisplayChanged = languageDisplayChanged,
            PublicationDisplayChanged = publicationDisplayChanged,
            SectionDisplayChanged = sectionDisplayChanged,
            TrackDisplayChanged = trackDisplayChanged,
            HasChanges = notifyLanguage || notifyPublication || notifySection || notifyTrack || notifyIsSectionVisible || displayTextOnlyChanged
        };

        // Update last values
        lastLanguageCode = currentLanguageCode;
        lastPublicationCode = currentPublicationCode;
        lastSectionNumber = currentSectionNumber;
        lastTrackNumber = currentTrackNumber;
        lastLanguageDisplayText = newLanguageDisplayText;
        lastPublicationDisplayText = newPublicationDisplayText;
        lastSectionDisplayText = newSectionDisplayText;
        lastTrackDisplayText = newTrackDisplayText;
        lastIsSectionVisible = newIsSectionVisible;

        return changeInfo;
    }

    public record PropertyChangeInfo
    {
        public string? CurrentLanguageCode { get; init; }
        public string? CurrentPublicationCode { get; init; }
        public int? CurrentSectionNumber { get; init; }
        public int? CurrentTrackNumber { get; init; }
        public string NewLanguageDisplayText { get; init; } = string.Empty;
        public string NewPublicationDisplayText { get; init; } = string.Empty;
        public string NewSectionDisplayText { get; init; } = string.Empty;
        public string NewTrackDisplayText { get; init; } = string.Empty;
        public bool NewIsSectionVisible { get; init; }
        public bool NotifyLanguage { get; init; }
        public bool NotifyPublication { get; init; }
        public bool NotifySection { get; init; }
        public bool NotifyTrack { get; init; }
        public bool NotifyIsSectionVisible { get; init; }
        public bool DisplayTextOnlyChanged { get; init; }
        public bool CascadeChangeOccurred { get; init; }
        public bool LanguageDisplayChanged { get; init; }
        public bool PublicationDisplayChanged { get; init; }
        public bool SectionDisplayChanged { get; init; }
        public bool TrackDisplayChanged { get; init; }
        public bool HasChanges { get; init; }
    }
}

