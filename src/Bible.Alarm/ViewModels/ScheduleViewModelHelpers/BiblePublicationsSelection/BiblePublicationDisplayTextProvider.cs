#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Checks if there are multiple languages available for the current category.
    /// Returns true if there are 2 or more languages, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsLanguageSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return false;
        }

        var categoryName = currentSchedule.BiblePublicationCategoryName;
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return false; // No category selected, can't determine
        }

        try
        {
            var languages = await mediaService.GetBiblePublicationLanguages(categoryName);
            return languages.Count > 1;
        }
        catch
        {
            return false; // On error, default to not selectable
        }
    }

    /// <summary>
    /// Checks if there are multiple publications available for the current language and category.
    /// Returns true if there are 2 or more publications, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsPublicationSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return false;
        }

        var languageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return false; // No category selected, can't determine
        }

        try
        {
            var publications = await mediaService.GetBiblePublications(languageCode, categoryName, downloadAll: false);
            // Only count downloaded publications (Id > 0), not placeholders (Id == 0)
            var downloadedCount = publications.Values.Count(p => p.Id > 0);
            return downloadedCount > 1;
        }
        catch
        {
            return false; // On error, default to not selectable
        }
    }

    /// <summary>
    /// Checks if there are multiple sections available for the current publication.
    /// Returns true if there are 2 or more sections, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsSectionSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            return false;
        }

        // If publication doesn't have sections, it's not selectable
        if (!GetIsSectionVisible())
        {
            return false;
        }

        var languageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;
        var publicationCode = currentSchedule.BiblePublicationCode;

        try
        {
            // Check if publication has LanguageId == null by checking if GetSectionsForPublicationWithoutLanguage returns results
            // We'll try both methods and see which one works
            SortedDictionary<int, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection> sections;
            
            // First try with language
            sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
            
            // If no sections found with language, try without language
            if (sections.Count == 0)
            {
                sections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
            }

            return sections.Count > 1;
        }
        catch
        {
            return false; // On error, default to not selectable
        }
    }

    /// <summary>
    /// Checks if there are multiple tracks available for the current section/publication.
    /// Returns true if there are 2 or more tracks, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsTrackSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            return false;
        }

        var languageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;
        var publicationCode = currentSchedule.BiblePublicationCode;
        var sectionNumber = currentSchedule.BiblePublicationSectionNumber ?? 0;

        try
        {
            // Try with language first
            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionNumber);
            
            // If no tracks found with language, try without language
            if (tracks.Count == 0)
            {
                tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionNumber);
            }

            return tracks.Count > 1;
        }
        catch
        {
            return false; // On error, default to not selectable
        }
    }
}

