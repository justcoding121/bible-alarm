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

        // Avoid DB calls in a UI getter.
        // Language row is hidden only for the special case where a specific publication is selected
        // and it has no language (LanguageCode is empty) - typically instrumental music publications.
        // Otherwise keep it visible; selectability is handled elsewhere.
        if (string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageCode);
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

        // If the row itself is hidden (e.g., selected publication has no language), it isn't selectable.
        if (!GetIsLanguageVisible())
        {
            return false;
        }

        // Avoid DB calls just to decide if the row is tappable.
        // In practice, Bible/Dramas/Music categories have multiple choices when language selection is applicable.
        if (string.Equals(categoryName, "Bible", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(categoryName, "Dramas", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
        {
            return true;
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

        // Avoid DB calls while category cascade is still populating language.
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
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
            SortedDictionary<int, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection> sections;
            
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                // Publication likely has no language FK (instrumental) OR cascade hasn't populated language yet.
                // Avoid a useless "with language" query.
                sections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
            }
            else
            {
                // First try with language
                sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
            
                // If no sections found with language, try without language
                if (sections.Count == 0)
                {
                    sections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
                }
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
        var sectionCode = currentSchedule.BiblePublicationSectionCode;
        var sectionIndex = SectionCodeHelper.GetSectionIndexOrZero(sectionCode);

        try
        {
            // Avoid duplicate queries when languageCode is empty.
            var tracks = string.IsNullOrWhiteSpace(languageCode)
                ? await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionIndex)
                : await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionIndex);
            
            // If no tracks found with language, try without language
            if (tracks.Count == 0 && !string.IsNullOrWhiteSpace(languageCode))
            {
                tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionIndex);
            }

            return tracks.Count > 1;
        }
        catch
        {
            return false; // On error, default to not selectable
        }
    }
}

