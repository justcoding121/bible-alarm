#nullable enable
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.Maui;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;

/// <summary>
/// Provides display text for bible-related properties.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePublicationDisplayTextProvider
{
    private readonly IState<ApplicationState> state;
    private readonly ILogger logger;

    public BiblePublicationDisplayTextProvider(IState<ApplicationState> state, ILogger logger)
    {
        this.state = state;
        this.logger = logger;
    }

    /// <summary>
    /// Determines if the section selection row should be visible.
    /// Returns false for dramas which don't have section selection.
    /// </summary>
    public bool GetIsSectionVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            return true; // Default to visible for traditional Bible reading
        }

        return PublicationTypeHelper.HasSectionStructure(currentSchedule.BiblePublicationCode);
    }

    /// <summary>
    /// Gets the language direction string ("ltr" or "rtl").
    /// </summary>
    public string GetLanguageDirection()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        return currentSchedule?.BiblePublicationLanguageDirection ?? "ltr";
    }

    /// <summary>
    /// Gets the FlowDirection based on the selected language direction.
    /// Returns RightToLeft for RTL languages, LeftToRight otherwise.
    /// </summary>
    public FlowDirection GetFlowDirection()
    {
        var direction = GetLanguageDirection();
        return string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    public string GetLanguageDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Read from CurrentSchedule for language name (populated during bootstrap/effects)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageName))
        {
            logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - Returning '{LanguageName}' (LanguageCode: {LanguageCode})",
                currentSchedule.BiblePublicationLanguageName, currentSchedule.BiblePublicationLanguageCode ?? "null");
            return currentSchedule.BiblePublicationLanguageName;
        }

        logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - CurrentSchedule is null or BiblePublicationLanguageName is empty. Returning empty string.");
        return string.Empty;
    }

    public string GetTranslationDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Read from CurrentSchedule for publication name (populated during bootstrap/effects)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationName))
        {
            return currentSchedule.BiblePublicationName;
        }

        // Fallback: use publication code from CurrentBiblePublicationSchedule or CurrentSchedule
        var biblePublication = state.Value.CurrentBiblePublicationSchedule;
        string publicationCode = biblePublication?.PublicationCode?.ToLowerInvariant()
            ?? currentSchedule?.BiblePublicationCode?.ToLowerInvariant()
            ?? string.Empty;

        if (string.IsNullOrEmpty(publicationCode))
        {
            return string.Empty;
        }

        // Format publication code using helper as fallback
        return PublicationDisplayHelper.GetDisplayName(publicationCode);
    }

    public string GetSectionDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Read from CurrentSchedule for section name (populated during bootstrap/effects)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationSectionName))
        {
            return currentSchedule.BiblePublicationSectionName;
        }

        return string.Empty;
    }

    public string GetTrackDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Determine the label based on publication type
        var label = PublicationTypeHelper.GetTrackLabel(currentSchedule?.BiblePublicationCode);

        // Read directly from CurrentBiblePublicationSchedule so it updates immediately when track changes
        var biblePublication = state.Value.CurrentBiblePublicationSchedule;
        if (biblePublication != null && biblePublication.TrackNumber > 0)
        {
            return $"{label} {biblePublication.TrackNumber}";
        }

        // Fallback to CurrentSchedule if CurrentBiblePublicationSchedule is not set (e.g., new schedule)
        if (currentSchedule != null &&
            currentSchedule.BiblePublicationTrackNumber.HasValue &&
            currentSchedule.BiblePublicationTrackNumber.Value > 0)
        {
            return $"{label} {currentSchedule.BiblePublicationTrackNumber.Value}";
        }

        return string.Empty;
    }
}

