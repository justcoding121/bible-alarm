#nullable enable
using Bible;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BibleSelection;

/// <summary>
/// Detects property changes in bible selection.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePublicationPropertyChangeDetector
{
    private readonly BiblePublicationDisplayTextProvider displayTextProvider;

    // Track last values to prevent unnecessary PropertyChanged notifications
    private string? lastBibleTypeDisplayText;
    private string? lastLanguageDisplayText;
    private string? lastTranslationDisplayText;
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
        lastBibleTypeDisplayText = displayTextProvider.GetBibleTypeDisplayText();
        lastLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        lastTranslationDisplayText = displayTextProvider.GetTranslationDisplayText();
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
        
        // Content type changes when publication code changes to/from a drama type
        var contentTypeChanged = publicationCodeChanged && 
            PublicationTypeHelper.IsDrama(currentPublicationCode) != PublicationTypeHelper.IsDrama(lastPublicationCode);

        var newBibleTypeDisplayText = displayTextProvider.GetBibleTypeDisplayText();
        var newLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        var newTranslationDisplayText = displayTextProvider.GetTranslationDisplayText();
        var newSectionDisplayText = displayTextProvider.GetSectionDisplayText();
        var newTrackDisplayText = displayTextProvider.GetTrackDisplayText();
        var newIsSectionVisible = displayTextProvider.GetIsSectionVisible();

        var bibleTypeDisplayChanged = newBibleTypeDisplayText != lastBibleTypeDisplayText;
        var languageDisplayChanged = newLanguageDisplayText != lastLanguageDisplayText;
        var translationDisplayChanged = newTranslationDisplayText != lastTranslationDisplayText;
        var sectionDisplayChanged = newSectionDisplayText != lastSectionDisplayText;
        var trackDisplayChanged = newTrackDisplayText != lastTrackDisplayText;
        var isSectionVisibleChanged = newIsSectionVisible != lastIsSectionVisible;

        // Content type change (publication code changed to/from drama) cascades to all properties below
        var notifyBibleType = contentTypeChanged;
        var notifyLanguage = contentTypeChanged || languageCodeChanged;
        var notifyTranslation = contentTypeChanged || languageCodeChanged || publicationCodeChanged;
        var notifySection = contentTypeChanged || languageCodeChanged || publicationCodeChanged || sectionNumberChanged;
        var notifyTrack = contentTypeChanged || languageCodeChanged || publicationCodeChanged || sectionNumberChanged || trackNumberChanged;
        var notifyIsSectionVisible = contentTypeChanged || isSectionVisibleChanged;

        var displayTextOnlyChanged = (bibleTypeDisplayChanged && !contentTypeChanged) ||
                                    (languageDisplayChanged && !contentTypeChanged && !languageCodeChanged) ||
                                    (translationDisplayChanged && !contentTypeChanged && !languageCodeChanged && !publicationCodeChanged) ||
                                    (sectionDisplayChanged && !contentTypeChanged && !languageCodeChanged && !publicationCodeChanged && !sectionNumberChanged) ||
                                    (trackDisplayChanged && !contentTypeChanged && !languageCodeChanged && !publicationCodeChanged && !sectionNumberChanged && !trackNumberChanged);

        var cascadeChangeOccurred = contentTypeChanged || languageCodeChanged || publicationCodeChanged || sectionNumberChanged || trackNumberChanged;

        var changeInfo = new PropertyChangeInfo
        {
            CurrentLanguageCode = currentLanguageCode,
            CurrentPublicationCode = currentPublicationCode,
            CurrentSectionNumber = currentSectionNumber,
            CurrentTrackNumber = currentTrackNumber,
            NewBibleTypeDisplayText = newBibleTypeDisplayText,
            NewLanguageDisplayText = newLanguageDisplayText,
            NewTranslationDisplayText = newTranslationDisplayText,
            NewSectionDisplayText = newSectionDisplayText,
            NewTrackDisplayText = newTrackDisplayText,
            NewIsSectionVisible = newIsSectionVisible,
            NotifyBibleType = notifyBibleType,
            NotifyLanguage = notifyLanguage,
            NotifyTranslation = notifyTranslation,
            NotifySection = notifySection,
            NotifyTrack = notifyTrack,
            NotifyIsSectionVisible = notifyIsSectionVisible,
            DisplayTextOnlyChanged = displayTextOnlyChanged,
            CascadeChangeOccurred = cascadeChangeOccurred,
            BibleTypeDisplayChanged = bibleTypeDisplayChanged,
            LanguageDisplayChanged = languageDisplayChanged,
            TranslationDisplayChanged = translationDisplayChanged,
            SectionDisplayChanged = sectionDisplayChanged,
            TrackDisplayChanged = trackDisplayChanged,
            HasChanges = notifyBibleType || notifyLanguage || notifyTranslation || notifySection || notifyTrack || notifyIsSectionVisible || displayTextOnlyChanged
        };

        // Update last values
        lastLanguageCode = currentLanguageCode;
        lastPublicationCode = currentPublicationCode;
        lastSectionNumber = currentSectionNumber;
        lastTrackNumber = currentTrackNumber;
        lastBibleTypeDisplayText = newBibleTypeDisplayText;
        lastLanguageDisplayText = newLanguageDisplayText;
        lastTranslationDisplayText = newTranslationDisplayText;
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
        public string NewBibleTypeDisplayText { get; init; } = string.Empty;
        public string NewLanguageDisplayText { get; init; } = string.Empty;
        public string NewTranslationDisplayText { get; init; } = string.Empty;
        public string NewSectionDisplayText { get; init; } = string.Empty;
        public string NewTrackDisplayText { get; init; } = string.Empty;
        public bool NewIsSectionVisible { get; init; }
        public bool NotifyBibleType { get; init; }
        public bool NotifyLanguage { get; init; }
        public bool NotifyTranslation { get; init; }
        public bool NotifySection { get; init; }
        public bool NotifyTrack { get; init; }
        public bool NotifyIsSectionVisible { get; init; }
        public bool DisplayTextOnlyChanged { get; init; }
        public bool CascadeChangeOccurred { get; init; }
        public bool BibleTypeDisplayChanged { get; init; }
        public bool LanguageDisplayChanged { get; init; }
        public bool TranslationDisplayChanged { get; init; }
        public bool SectionDisplayChanged { get; init; }
        public bool TrackDisplayChanged { get; init; }
        public bool HasChanges { get; init; }
    }
}

