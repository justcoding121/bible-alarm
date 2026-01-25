#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
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
    private readonly IMediaService mediaService;

    public BiblePublicationDisplayTextProvider(IState<ApplicationState> state, ILogger logger, IMediaService mediaService)
    {
        this.state = state;
        this.logger = logger;
        this.mediaService = mediaService;
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

        // Return placeholder text if category is selected but language is not
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCategoryName))
        {
            logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - CurrentSchedule is null or BiblePublicationLanguageName is empty. Returning placeholder text.");
            return "Select Language";
        }

        logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - CurrentSchedule is null or BiblePublicationLanguageName is empty. Returning empty string.");
        return string.Empty;
    }

    public string GetPublicationDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Read from CurrentSchedule for publication name (populated during bootstrap/effects)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationName))
        {
            return currentSchedule.BiblePublicationName;
        }

        // Use CurrentSchedule as the source of truth
        string publicationCode = currentSchedule?.BiblePublicationCode?.ToLowerInvariant()
            ?? string.Empty;

        if (!string.IsNullOrEmpty(publicationCode))
        {
            // Format publication code using helper as fallback
            return PublicationDisplayHelper.GetDisplayName(publicationCode);
        }

        // Return placeholder text if category is selected but publication is not
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCategoryName))
        {
            return "Select Publication";
        }

        return string.Empty;
    }

    public string GetSectionDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Read from CurrentSchedule for section name (populated during bootstrap/effects)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationSectionName))
        {
            return currentSchedule.BiblePublicationSectionName;
        }

        // Return placeholder text if publication is selected but section is not
        // Only show placeholder if section row is visible (i.e., for sectioned publications)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            if (GetIsSectionVisible())
            {
                return "Select Section";
            }
        }

        return string.Empty;
    }

    public string GetTrackDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Always show the track title from DB (populated during bootstrap/selection)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationTrackTitle))
        {
            return currentSchedule.BiblePublicationTrackTitle;
        }

        // Return placeholder text if publication is selected but track is not
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            // For non-sectioned publications (dramas), show "Select Episode"
            // For sectioned publications, show "Select Track"
            bool hasSectionStructure = PublicationTypeHelper.HasSectionStructure(currentSchedule.BiblePublicationCode);
            
            return hasSectionStructure ? "Select Track" : "Select Episode";
        }

        return string.Empty;
    }

    public string GetCategoryDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCategoryName))
        {
            return currentSchedule.BiblePublicationCategoryName;
        }

        return string.Empty;
    }

    /// <summary>
    /// Determines if the language row should be visible.
    /// Returns false when ALL publications in the category have LanguageId == null (no language FK).
    /// This is data-driven - checks if PublicationLanguages table has any entries for this category.
    /// If PublicationLanguages has entries, it means there are publications with languages.
    /// If PublicationLanguages is empty for this category, it means all publications have LanguageId == null.
    /// </summary>
    public bool GetIsLanguageVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return true; // Default to visible
        }

        var categoryName = currentSchedule.BiblePublicationCategoryName;
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return true; // No category selected, show language row
        }

        // Data-driven check: If PublicationLanguages has any entries for this category,
        // it means there are publications with languages, so show the language row.
        // If PublicationLanguages is empty for this category, all publications have LanguageId == null, so hide it.
        // This checks the discovery table (PublicationLanguages) which is faster than querying BiblePublications.
        try
        {
            // Use Task.Run to avoid blocking, but we need to wait for the result
            // This is acceptable since it's only called when the UI needs to determine visibility
            // and GetBiblePublicationLanguages queries PublicationLanguages which is fast
            var languages = Task.Run(async () => 
                await mediaService.GetBiblePublicationLanguages(categoryName)).GetAwaiter().GetResult();
            
            // If no languages found in PublicationLanguages, it means all publications in this category have LanguageId == null
            return languages.Count > 0;
        }
        catch
        {
            // On error, default to visible to be safe
            return true;
        }
    }
}

