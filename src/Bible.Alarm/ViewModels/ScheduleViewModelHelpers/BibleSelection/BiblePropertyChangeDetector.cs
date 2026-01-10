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
    private string? lastBookDisplayText;
    private string? lastChapterDisplayText;
    private bool? lastIsBookVisible;

    // Track underlying property values to detect cascading changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private int? lastBookNumber;
    private int? lastChapterNumber;

    public BiblePropertyChangeDetector(BibleDisplayTextProvider displayTextProvider)
    {
        this.displayTextProvider = displayTextProvider;
    }

    public void Initialize(
        string? languageCode,
        string? publicationCode,
        int? bookNumber,
        int? chapterNumber)
    {
        lastLanguageCode = languageCode;
        lastPublicationCode = publicationCode;
        lastBookNumber = bookNumber;
        lastChapterNumber = chapterNumber;
        lastBibleTypeDisplayText = displayTextProvider.GetBibleTypeDisplayText();
        lastLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        lastTranslationDisplayText = displayTextProvider.GetTranslationDisplayText();
        lastBookDisplayText = displayTextProvider.GetBookDisplayText();
        lastChapterDisplayText = displayTextProvider.GetChapterDisplayText();
        lastIsBookVisible = displayTextProvider.GetIsBookVisible();
    }

    public PropertyChangeInfo DetectPropertyChanges(ScheduleStateItem? currentSchedule, BibleReadingStateItem? currentBibleReading)
    {
        var currentLanguageCode = currentSchedule?.BibleReadingLanguageCode;
        var currentPublicationCode = currentBibleReading?.PublicationCode ?? currentSchedule?.BibleReadingPublicationCode;
        var currentBookNumber = currentBibleReading?.BookNumber ?? currentSchedule?.BibleReadingBookNumber;
        var currentChapterNumber = currentBibleReading?.ChapterNumber ?? currentSchedule?.BibleReadingChapterNumber;

        var languageCodeChanged = currentLanguageCode != lastLanguageCode;
        var publicationCodeChanged = currentPublicationCode != lastPublicationCode;
        var bookNumberChanged = currentBookNumber != lastBookNumber;
        var chapterNumberChanged = currentChapterNumber != lastChapterNumber;
        
        // Content type changes when publication code changes to/from a drama type
        var contentTypeChanged = publicationCodeChanged && 
            PublicationTypeHelper.IsDrama(currentPublicationCode) != PublicationTypeHelper.IsDrama(lastPublicationCode);

        var newBibleTypeDisplayText = displayTextProvider.GetBibleTypeDisplayText();
        var newLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        var newTranslationDisplayText = displayTextProvider.GetTranslationDisplayText();
        var newBookDisplayText = displayTextProvider.GetBookDisplayText();
        var newChapterDisplayText = displayTextProvider.GetChapterDisplayText();
        var newIsBookVisible = displayTextProvider.GetIsBookVisible();

        var bibleTypeDisplayChanged = newBibleTypeDisplayText != lastBibleTypeDisplayText;
        var languageDisplayChanged = newLanguageDisplayText != lastLanguageDisplayText;
        var translationDisplayChanged = newTranslationDisplayText != lastTranslationDisplayText;
        var bookDisplayChanged = newBookDisplayText != lastBookDisplayText;
        var chapterDisplayChanged = newChapterDisplayText != lastChapterDisplayText;
        var isBookVisibleChanged = newIsBookVisible != lastIsBookVisible;

        // Content type change (publication code changed to/from drama) cascades to all properties below
        var notifyBibleType = contentTypeChanged;
        var notifyLanguage = contentTypeChanged || languageCodeChanged;
        var notifyTranslation = contentTypeChanged || languageCodeChanged || publicationCodeChanged;
        var notifyBook = contentTypeChanged || languageCodeChanged || publicationCodeChanged || bookNumberChanged;
        var notifyChapter = contentTypeChanged || languageCodeChanged || publicationCodeChanged || bookNumberChanged || chapterNumberChanged;
        var notifyIsBookVisible = contentTypeChanged || isBookVisibleChanged;

        var displayTextOnlyChanged = (bibleTypeDisplayChanged && !contentTypeChanged) ||
                                    (languageDisplayChanged && !contentTypeChanged && !languageCodeChanged) ||
                                    (translationDisplayChanged && !contentTypeChanged && !languageCodeChanged && !publicationCodeChanged) ||
                                    (bookDisplayChanged && !contentTypeChanged && !languageCodeChanged && !publicationCodeChanged && !bookNumberChanged) ||
                                    (chapterDisplayChanged && !contentTypeChanged && !languageCodeChanged && !publicationCodeChanged && !bookNumberChanged && !chapterNumberChanged);

        var cascadeChangeOccurred = contentTypeChanged || languageCodeChanged || publicationCodeChanged || bookNumberChanged || chapterNumberChanged;

        var changeInfo = new PropertyChangeInfo
        {
            CurrentLanguageCode = currentLanguageCode,
            CurrentPublicationCode = currentPublicationCode,
            CurrentBookNumber = currentBookNumber,
            CurrentChapterNumber = currentChapterNumber,
            NewBibleTypeDisplayText = newBibleTypeDisplayText,
            NewLanguageDisplayText = newLanguageDisplayText,
            NewTranslationDisplayText = newTranslationDisplayText,
            NewBookDisplayText = newBookDisplayText,
            NewChapterDisplayText = newChapterDisplayText,
            NewIsBookVisible = newIsBookVisible,
            NotifyBibleType = notifyBibleType,
            NotifyLanguage = notifyLanguage,
            NotifyTranslation = notifyTranslation,
            NotifyBook = notifyBook,
            NotifyChapter = notifyChapter,
            NotifyIsBookVisible = notifyIsBookVisible,
            DisplayTextOnlyChanged = displayTextOnlyChanged,
            CascadeChangeOccurred = cascadeChangeOccurred,
            BibleTypeDisplayChanged = bibleTypeDisplayChanged,
            LanguageDisplayChanged = languageDisplayChanged,
            TranslationDisplayChanged = translationDisplayChanged,
            BookDisplayChanged = bookDisplayChanged,
            ChapterDisplayChanged = chapterDisplayChanged,
            HasChanges = notifyBibleType || notifyLanguage || notifyTranslation || notifyBook || notifyChapter || notifyIsBookVisible || displayTextOnlyChanged
        };

        // Update last values
        lastLanguageCode = currentLanguageCode;
        lastPublicationCode = currentPublicationCode;
        lastBookNumber = currentBookNumber;
        lastChapterNumber = currentChapterNumber;
        lastBibleTypeDisplayText = newBibleTypeDisplayText;
        lastLanguageDisplayText = newLanguageDisplayText;
        lastTranslationDisplayText = newTranslationDisplayText;
        lastBookDisplayText = newBookDisplayText;
        lastChapterDisplayText = newChapterDisplayText;
        lastIsBookVisible = newIsBookVisible;

        return changeInfo;
    }

    public record PropertyChangeInfo
    {
        public string? CurrentLanguageCode { get; init; }
        public string? CurrentPublicationCode { get; init; }
        public int? CurrentBookNumber { get; init; }
        public int? CurrentChapterNumber { get; init; }
        public string NewBibleTypeDisplayText { get; init; } = string.Empty;
        public string NewLanguageDisplayText { get; init; } = string.Empty;
        public string NewTranslationDisplayText { get; init; } = string.Empty;
        public string NewBookDisplayText { get; init; } = string.Empty;
        public string NewChapterDisplayText { get; init; } = string.Empty;
        public bool NewIsBookVisible { get; init; }
        public bool NotifyBibleType { get; init; }
        public bool NotifyLanguage { get; init; }
        public bool NotifyTranslation { get; init; }
        public bool NotifyBook { get; init; }
        public bool NotifyChapter { get; init; }
        public bool NotifyIsBookVisible { get; init; }
        public bool DisplayTextOnlyChanged { get; init; }
        public bool CascadeChangeOccurred { get; init; }
        public bool BibleTypeDisplayChanged { get; init; }
        public bool LanguageDisplayChanged { get; init; }
        public bool TranslationDisplayChanged { get; init; }
        public bool BookDisplayChanged { get; init; }
        public bool ChapterDisplayChanged { get; init; }
        public bool HasChanges { get; init; }
    }
}

