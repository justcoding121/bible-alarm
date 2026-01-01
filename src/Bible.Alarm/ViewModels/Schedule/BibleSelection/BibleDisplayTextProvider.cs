#nullable enable
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule.BibleSelection;

/// <summary>
/// Provides display text for bible-related properties.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BibleDisplayTextProvider
{
    private readonly IState<ApplicationState> state;
    private readonly ILogger logger;

    public BibleDisplayTextProvider(IState<ApplicationState> state, ILogger logger)
    {
        this.state = state;
        this.logger = logger;
    }

    public string GetLanguageDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Read from CurrentSchedule for language name (populated during bootstrap/effects)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BibleReadingLanguageName))
        {
            logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - Returning '{LanguageName}' (LanguageCode: {LanguageCode})",
                currentSchedule.BibleReadingLanguageName, currentSchedule.BibleReadingLanguageCode ?? "null");
            return currentSchedule.BibleReadingLanguageName;
        }

        logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - CurrentSchedule is null or BibleReadingLanguageName is empty. Returning empty string.");
        return string.Empty;
    }

    public string GetTranslationDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Read from CurrentSchedule for publication name (populated during bootstrap/effects)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BibleReadingPublicationName))
        {
            return currentSchedule.BibleReadingPublicationName;
        }

        // Fallback: use publication code from CurrentBibleReadingSchedule or CurrentSchedule
        var bibleReading = state.Value.CurrentBibleReadingSchedule;
        string publicationCode = bibleReading?.PublicationCode?.ToLowerInvariant()
            ?? currentSchedule?.BibleReadingPublicationCode?.ToLowerInvariant()
            ?? string.Empty;

        if (string.IsNullOrEmpty(publicationCode))
        {
            return string.Empty;
        }

        // Format publication code using helper as fallback
        return PublicationDisplayHelper.GetDisplayName(publicationCode);
    }

    public string GetBookDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Read from CurrentSchedule for book name (populated during bootstrap/effects)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BibleReadingBookName))
        {
            return currentSchedule.BibleReadingBookName;
        }

        return string.Empty;
    }

    public string GetChapterDisplayText()
    {
        // Read directly from CurrentBibleReadingSchedule so it updates immediately when chapter changes
        var bibleReading = state.Value.CurrentBibleReadingSchedule;
        if (bibleReading != null && bibleReading.ChapterNumber > 0)
        {
            // ChapterNumber is always valid (1-150) if BibleReadingSchedule exists
            return $"Chapter {bibleReading.ChapterNumber}";
        }

        // Fallback to CurrentSchedule if CurrentBibleReadingSchedule is not set (e.g., new schedule)
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null &&
            currentSchedule.BibleReadingChapterNumber.HasValue &&
            currentSchedule.BibleReadingChapterNumber.Value > 0)
        {
            return $"Chapter {currentSchedule.BibleReadingChapterNumber.Value}";
        }

        return string.Empty;
    }
}

