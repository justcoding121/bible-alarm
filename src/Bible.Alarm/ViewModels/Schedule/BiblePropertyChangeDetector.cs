#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.Schedule;

/// <summary>
/// Detects property changes in bible selection.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePropertyChangeDetector
{
    private readonly BibleDisplayTextProvider displayTextProvider;

    // Track last values to prevent unnecessary PropertyChanged notifications
    private string? lastLanguageDisplayText;
    private string? lastTranslationDisplayText;
    private string? lastBookDisplayText;
    private string? lastChapterDisplayText;

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
        lastLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        lastTranslationDisplayText = displayTextProvider.GetTranslationDisplayText();
        lastBookDisplayText = displayTextProvider.GetBookDisplayText();
        lastChapterDisplayText = displayTextProvider.GetChapterDisplayText();
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

        var newLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        var newTranslationDisplayText = displayTextProvider.GetTranslationDisplayText();
        var newBookDisplayText = displayTextProvider.GetBookDisplayText();
        var newChapterDisplayText = displayTextProvider.GetChapterDisplayText();

        var languageDisplayChanged = newLanguageDisplayText != lastLanguageDisplayText;
        var translationDisplayChanged = newTranslationDisplayText != lastTranslationDisplayText;
        var bookDisplayChanged = newBookDisplayText != lastBookDisplayText;
        var chapterDisplayChanged = newChapterDisplayText != lastChapterDisplayText;

        var notifyLanguage = languageCodeChanged;
        var notifyTranslation = languageCodeChanged || publicationCodeChanged;
        var notifyBook = languageCodeChanged || publicationCodeChanged || bookNumberChanged;
        var notifyChapter = languageCodeChanged || publicationCodeChanged || bookNumberChanged || chapterNumberChanged;

        var displayTextOnlyChanged = (languageDisplayChanged && !languageCodeChanged) ||
                                    (translationDisplayChanged && !languageCodeChanged && !publicationCodeChanged) ||
                                    (bookDisplayChanged && !languageCodeChanged && !publicationCodeChanged && !bookNumberChanged) ||
                                    (chapterDisplayChanged && !languageCodeChanged && !publicationCodeChanged && !bookNumberChanged && !chapterNumberChanged);

        var cascadeChangeOccurred = languageCodeChanged || publicationCodeChanged || bookNumberChanged || chapterNumberChanged;

        var changeInfo = new PropertyChangeInfo
        {
            CurrentLanguageCode = currentLanguageCode,
            CurrentPublicationCode = currentPublicationCode,
            CurrentBookNumber = currentBookNumber,
            CurrentChapterNumber = currentChapterNumber,
            NewLanguageDisplayText = newLanguageDisplayText,
            NewTranslationDisplayText = newTranslationDisplayText,
            NewBookDisplayText = newBookDisplayText,
            NewChapterDisplayText = newChapterDisplayText,
            NotifyLanguage = notifyLanguage,
            NotifyTranslation = notifyTranslation,
            NotifyBook = notifyBook,
            NotifyChapter = notifyChapter,
            DisplayTextOnlyChanged = displayTextOnlyChanged,
            CascadeChangeOccurred = cascadeChangeOccurred,
            LanguageDisplayChanged = languageDisplayChanged,
            TranslationDisplayChanged = translationDisplayChanged,
            BookDisplayChanged = bookDisplayChanged,
            ChapterDisplayChanged = chapterDisplayChanged,
            HasChanges = notifyLanguage || notifyTranslation || notifyBook || notifyChapter || displayTextOnlyChanged
        };

        // Update last values
        lastLanguageCode = currentLanguageCode;
        lastPublicationCode = currentPublicationCode;
        lastBookNumber = currentBookNumber;
        lastChapterNumber = currentChapterNumber;
        lastLanguageDisplayText = newLanguageDisplayText;
        lastTranslationDisplayText = newTranslationDisplayText;
        lastBookDisplayText = newBookDisplayText;
        lastChapterDisplayText = newChapterDisplayText;

        return changeInfo;
    }

    public record PropertyChangeInfo
    {
        public string? CurrentLanguageCode { get; init; }
        public string? CurrentPublicationCode { get; init; }
        public int? CurrentBookNumber { get; init; }
        public int? CurrentChapterNumber { get; init; }
        public string NewLanguageDisplayText { get; init; } = string.Empty;
        public string NewTranslationDisplayText { get; init; } = string.Empty;
        public string NewBookDisplayText { get; init; } = string.Empty;
        public string NewChapterDisplayText { get; init; } = string.Empty;
        public bool NotifyLanguage { get; init; }
        public bool NotifyTranslation { get; init; }
        public bool NotifyBook { get; init; }
        public bool NotifyChapter { get; init; }
        public bool DisplayTextOnlyChanged { get; init; }
        public bool CascadeChangeOccurred { get; init; }
        public bool LanguageDisplayChanged { get; init; }
        public bool TranslationDisplayChanged { get; init; }
        public bool BookDisplayChanged { get; init; }
        public bool ChapterDisplayChanged { get; init; }
        public bool HasChanges { get; init; }
    }
}

