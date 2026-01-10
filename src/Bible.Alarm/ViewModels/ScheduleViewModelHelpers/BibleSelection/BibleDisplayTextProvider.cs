#nullable enable
using Bible;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BibleSelection;

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

    /// <summary>
    /// Gets the display text for the content type (Bible Reading or Drama type).
    /// </summary>
    public string GetBibleTypeDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.BibleReadingPublicationCode))
        {
            return "Bible Reading";
        }

        return PublicationTypeHelper.GetContentTypeDisplayName(currentSchedule.BibleReadingPublicationCode);
    }

    /// <summary>
    /// Determines if the book selection row should be visible.
    /// Returns false for dramas which don't have book selection.
    /// </summary>
    public bool GetIsBookVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.BibleReadingPublicationCode))
        {
            return true; // Default to visible for traditional Bible reading
        }

        return PublicationTypeHelper.HasBookStructure(currentSchedule.BibleReadingPublicationCode);
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
        var currentSchedule = state.Value.CurrentSchedule;

        // Determine the label based on publication type
        var label = PublicationTypeHelper.GetChapterLabel(currentSchedule?.BibleReadingPublicationCode);

        // Read directly from CurrentBibleReadingSchedule so it updates immediately when chapter changes
        var bibleReading = state.Value.CurrentBibleReadingSchedule;
        if (bibleReading != null && bibleReading.ChapterNumber > 0)
        {
            return $"{label} {bibleReading.ChapterNumber}";
        }

        // Fallback to CurrentSchedule if CurrentBibleReadingSchedule is not set (e.g., new schedule)
        if (currentSchedule != null &&
            currentSchedule.BibleReadingChapterNumber.HasValue &&
            currentSchedule.BibleReadingChapterNumber.Value > 0)
        {
            return $"{label} {currentSchedule.BibleReadingChapterNumber.Value}";
        }

        return string.Empty;
    }
}

