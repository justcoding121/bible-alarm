#nullable enable
using Bible;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BibleSelection;

/// <summary>
/// Detects property changes in bible selection.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePropertyChangeDetector
{
    private readonly BibleDisplayTextProvider displayTextProvider;

    // Track last values to prevent unnecessary PropertyChanged notifications
    private string? lastBibleTypeDisplayText;
    private string? lastLanguageDisplayText;
    private string? lastTranslationDisplayText;
    private string? lastSectionDisplayText;
    private string? lastChapterDisplayText;
    private bool? lastIsSectionVisible;

    // Track underlying property values to detect cascading changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private int? lastSectionNumber;
    private int? lastChapterNumber;

    public BiblePropertyChangeDetector(BibleDisplayTextProvider displayTextProvider)
    {
        this.displayTextProvider = displayTextProvider;
    }

    public void Initialize(
        string? languageCode,
        string? publicationCode,
        int? sectionNumber,
        int? chapterNumber)
    {
        lastLanguageCode = languageCode;
        lastPublicationCode = publicationCode;
        lastSectionNumber = sectionNumber;
        lastChapterNumber = chapterNumber;
        lastBibleTypeDisplayText = displayTextProvider.GetBibleTypeDisplayText();
        lastLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        lastTranslationDisplayText = displayTextProvider.GetTranslationDisplayText();
        lastSectionDisplayText = displayTextProvider.GetSectionDisplayText();
        lastChapterDisplayText = displayTextProvider.GetChapterDisplayText();
        lastIsSectionVisible = displayTextProvider.GetIsSectionVisible();
    }

    public PropertyChangeInfo DetectPropertyChanges(ScheduleStateItem? currentSchedule, BibleReadingStateItem? currentBibleReading)
    {
        var currentLanguageCode = currentSchedule?.BibleReadingLanguageCode;
        var currentPublicationCode = currentBibleReading?.PublicationCode ?? currentSchedule?.BibleReadingPublicationCode;
        var currentSectionNumber = currentBibleReading?.SectionNumber ?? currentSchedule?.BibleReadingSectionNumber;
        var currentChapterNumber = currentBibleReading?.ChapterNumber ?? currentSchedule?.BibleReadingChapterNumber;

        var languageCodeChanged = currentLanguageCode != lastLanguageCode;
        var publicationCodeChanged = currentPublicationCode != lastPublicationCode;
        var sectionNumberChanged = currentSectionNumber != lastSectionNumber;
        var chapterNumberChanged = currentChapterNumber != lastChapterNumber;
        
        // Content type changes when publication code changes to/from a drama type
        var contentTypeChanged = publicationCodeChanged && 
            PublicationTypeHelper.IsDrama(currentPublicationCode) != PublicationTypeHelper.IsDrama(lastPublicationCode);

        var newBibleTypeDisplayText = displayTextProvider.GetBibleTypeDisplayText();
        var newLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        var newTranslationDisplayText = displayTextProvider.GetTranslationDisplayText();
        var newSectionDisplayText = displayTextProvider.GetSectionDisplayText();
        var newChapterDisplayText = displayTextProvider.GetChapterDisplayText();
        var newIsSectionVisible = displayTextProvider.GetIsSectionVisible();

        var bibleTypeDisplayChanged = newBibleTypeDisplayText != lastBibleTypeDisplayText;
        var languageDisplayChanged = newLanguageDisplayText != lastLanguageDisplayText;
        var translationDisplayChanged = newTranslationDisplayText != lastTranslationDisplayText;
        var sectionDisplayChanged = newSectionDisplayText != lastSectionDisplayText;
        var chapterDisplayChanged = newChapterDisplayText != lastChapterDisplayText;
        var isSectionVisibleChanged = newIsSectionVisible != lastIsSectionVisible;

        // Content type change (publication code changed to/from drama) cascades to all properties below
        var notifyBibleType = contentTypeChanged;
        var notifyLanguage = contentTypeChanged || languageCodeChanged;
        var notifyTranslation = contentTypeChanged || languageCodeChanged || publicationCodeChanged;
        var notifySection = contentTypeChanged || languageCodeChanged || publicationCodeChanged || sectionNumberChanged;
        var notifyChapter = contentTypeChanged || languageCodeChanged || publicationCodeChanged || sectionNumberChanged || chapterNumberChanged;
        var notifyIsSectionVisible = contentTypeChanged || isSectionVisibleChanged;

        var displayTextOnlyChanged = (bibleTypeDisplayChanged && !contentTypeChanged) ||
                                    (languageDisplayChanged && !contentTypeChanged && !languageCodeChanged) ||
                                    (translationDisplayChanged && !contentTypeChanged && !languageCodeChanged && !publicationCodeChanged) ||
                                    (sectionDisplayChanged && !contentTypeChanged && !languageCodeChanged && !publicationCodeChanged && !sectionNumberChanged) ||
                                    (chapterDisplayChanged && !contentTypeChanged && !languageCodeChanged && !publicationCodeChanged && !sectionNumberChanged && !chapterNumberChanged);

        var cascadeChangeOccurred = contentTypeChanged || languageCodeChanged || publicationCodeChanged || sectionNumberChanged || chapterNumberChanged;

        var changeInfo = new PropertyChangeInfo
        {
            CurrentLanguageCode = currentLanguageCode,
            CurrentPublicationCode = currentPublicationCode,
            CurrentSectionNumber = currentSectionNumber,
            CurrentChapterNumber = currentChapterNumber,
            NewBibleTypeDisplayText = newBibleTypeDisplayText,
            NewLanguageDisplayText = newLanguageDisplayText,
            NewTranslationDisplayText = newTranslationDisplayText,
            NewSectionDisplayText = newSectionDisplayText,
            NewChapterDisplayText = newChapterDisplayText,
            NewIsSectionVisible = newIsSectionVisible,
            NotifyBibleType = notifyBibleType,
            NotifyLanguage = notifyLanguage,
            NotifyTranslation = notifyTranslation,
            NotifySection = notifySection,
            NotifyChapter = notifyChapter,
            NotifyIsSectionVisible = notifyIsSectionVisible,
            DisplayTextOnlyChanged = displayTextOnlyChanged,
            CascadeChangeOccurred = cascadeChangeOccurred,
            BibleTypeDisplayChanged = bibleTypeDisplayChanged,
            LanguageDisplayChanged = languageDisplayChanged,
            TranslationDisplayChanged = translationDisplayChanged,
            SectionDisplayChanged = sectionDisplayChanged,
            ChapterDisplayChanged = chapterDisplayChanged,
            HasChanges = notifyBibleType || notifyLanguage || notifyTranslation || notifySection || notifyChapter || notifyIsSectionVisible || displayTextOnlyChanged
        };

        // Update last values
        lastLanguageCode = currentLanguageCode;
        lastPublicationCode = currentPublicationCode;
        lastSectionNumber = currentSectionNumber;
        lastChapterNumber = currentChapterNumber;
        lastBibleTypeDisplayText = newBibleTypeDisplayText;
        lastLanguageDisplayText = newLanguageDisplayText;
        lastTranslationDisplayText = newTranslationDisplayText;
        lastSectionDisplayText = newSectionDisplayText;
        lastChapterDisplayText = newChapterDisplayText;
        lastIsSectionVisible = newIsSectionVisible;

        return changeInfo;
    }

    public record PropertyChangeInfo
    {
        public string? CurrentLanguageCode { get; init; }
        public string? CurrentPublicationCode { get; init; }
        public int? CurrentSectionNumber { get; init; }
        public int? CurrentChapterNumber { get; init; }
        public string NewBibleTypeDisplayText { get; init; } = string.Empty;
        public string NewLanguageDisplayText { get; init; } = string.Empty;
        public string NewTranslationDisplayText { get; init; } = string.Empty;
        public string NewSectionDisplayText { get; init; } = string.Empty;
        public string NewChapterDisplayText { get; init; } = string.Empty;
        public bool NewIsSectionVisible { get; init; }
        public bool NotifyBibleType { get; init; }
        public bool NotifyLanguage { get; init; }
        public bool NotifyTranslation { get; init; }
        public bool NotifySection { get; init; }
        public bool NotifyChapter { get; init; }
        public bool NotifyIsSectionVisible { get; init; }
        public bool DisplayTextOnlyChanged { get; init; }
        public bool CascadeChangeOccurred { get; init; }
        public bool BibleTypeDisplayChanged { get; init; }
        public bool LanguageDisplayChanged { get; init; }
        public bool TranslationDisplayChanged { get; init; }
        public bool SectionDisplayChanged { get; init; }
        public bool ChapterDisplayChanged { get; init; }
        public bool HasChanges { get; init; }
    }
}

