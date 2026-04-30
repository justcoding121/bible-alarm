#nullable enable
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;

/// <summary>
/// Detects property changes in bible selection.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePublicationPropertyChangeDetector
{
    private readonly BiblePublicationDisplayTextProvider displayTextProvider;

    // Track last values to prevent unnecessary PropertyChanged notifications
    private string? lastCategoryDisplayText;
    private string? lastLanguageDisplayText;
    private string? lastPublicationDisplayText;
    private string? lastSectionDisplayText;
    private string? lastTrackDisplayText;
    private bool? lastIsSectionVisible;
    private bool? lastIsLanguageVisible;

    // Track underlying property values to detect cascading changes
    private int? lastCategoryId;
    private string? lastCategoryName;
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private string? lastSectionCode;
    private string? lastTrackCode;

    public BiblePublicationPropertyChangeDetector(BiblePublicationDisplayTextProvider displayTextProvider)
    {
        this.displayTextProvider = displayTextProvider;
    }

    public void Initialize(
        int? categoryId,
        string? categoryName,
        string? languageCode,
        string? publicationCode,
        string? sectionCode,
        string? trackCode)
    {
        lastCategoryId = categoryId;
        lastCategoryName = categoryName;
        lastLanguageCode = languageCode;
        lastPublicationCode = publicationCode;
        lastSectionCode = sectionCode;
        lastTrackCode = trackCode;
        lastCategoryDisplayText = displayTextProvider.GetCategoryDisplayText();
        lastLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        lastPublicationDisplayText = displayTextProvider.GetPublicationDisplayText();
        lastSectionDisplayText = displayTextProvider.GetSectionDisplayText();
        lastTrackDisplayText = displayTextProvider.GetTrackDisplayText();
        lastIsSectionVisible = displayTextProvider.GetIsSectionVisible();
        lastIsLanguageVisible = BiblePublicationDisplayTextProvider.IsLanguageVisible;
    }

    public PropertyChangeInfo DetectPropertyChanges(ScheduleStateItem? currentSchedule)
    {
        // Use CurrentSchedule as the single source of truth
        var currentCategoryId = currentSchedule?.BiblePublicationCategoryId;
        var currentCategoryName = currentSchedule?.BiblePublicationCategoryName;
        var currentLanguageCode = currentSchedule?.BiblePublicationLanguageCode;
        var currentPublicationCode = currentSchedule?.BiblePublicationCode;
        var currentSectionCode = currentSchedule?.BiblePublicationSectionCode;
        var currentTrackCode = currentSchedule?.BiblePublicationTrackCode;

        var categoryIdChanged = currentCategoryId != lastCategoryId;
        var categoryNameChanged = currentCategoryName != lastCategoryName;
        var languageCodeChanged = currentLanguageCode != lastLanguageCode;
        var publicationCodeChanged = currentPublicationCode != lastPublicationCode;
        var sectionCodeChanged = currentSectionCode != lastSectionCode;
        var trackCodeChanged = currentTrackCode != lastTrackCode;

        // Section visibility changes when publication code changes to/from a drama type
        var sectionVisibilityTypeChanged = publicationCodeChanged &&
            PublicationTypeHelper.IsDrama(currentPublicationCode) != PublicationTypeHelper.IsDrama(lastPublicationCode);

        var newCategoryDisplayText = displayTextProvider.GetCategoryDisplayText();
        var newLanguageDisplayText = displayTextProvider.GetLanguageDisplayText();
        var newPublicationDisplayText = displayTextProvider.GetPublicationDisplayText();
        var newSectionDisplayText = displayTextProvider.GetSectionDisplayText();
        var newTrackDisplayText = displayTextProvider.GetTrackDisplayText();
        var newIsSectionVisible = displayTextProvider.GetIsSectionVisible();
        var newIsLanguageVisible = BiblePublicationDisplayTextProvider.IsLanguageVisible;

        var categoryDisplayChanged = newCategoryDisplayText != lastCategoryDisplayText;
        var languageDisplayChanged = newLanguageDisplayText != lastLanguageDisplayText;
        var publicationDisplayChanged = newPublicationDisplayText != lastPublicationDisplayText;
        var sectionDisplayChanged = newSectionDisplayText != lastSectionDisplayText;
        var trackDisplayChanged = newTrackDisplayText != lastTrackDisplayText;
        var isSectionVisibleChanged = newIsSectionVisible != lastIsSectionVisible;
        var isLanguageVisibleChanged = newIsLanguageVisible != lastIsLanguageVisible;

        // Cascade logic: Category → Language → Publication → Section → Track
        // Category change cascades to everything below
        // IsSectionVisible changes when publication type changes (to/from drama)
        var notifyCategory = categoryIdChanged || categoryNameChanged;
        var notifyLanguage = categoryIdChanged || categoryNameChanged || languageCodeChanged;
        var notifyPublication = categoryIdChanged || categoryNameChanged || languageCodeChanged || publicationCodeChanged;
        var notifySection = categoryIdChanged || categoryNameChanged || languageCodeChanged || publicationCodeChanged || sectionCodeChanged;
        var notifyTrack = categoryIdChanged || categoryNameChanged || languageCodeChanged || publicationCodeChanged || sectionCodeChanged || trackCodeChanged;
        var notifyIsSectionVisible = sectionVisibilityTypeChanged || isSectionVisibleChanged;
        var notifyIsLanguageVisible = categoryIdChanged || categoryNameChanged || isLanguageVisibleChanged;

        var displayTextOnlyChanged = (categoryDisplayChanged && !categoryIdChanged && !categoryNameChanged) ||
                                    (languageDisplayChanged && !categoryIdChanged && !categoryNameChanged && !languageCodeChanged) ||
                                    (publicationDisplayChanged && !categoryIdChanged && !categoryNameChanged && !languageCodeChanged && !publicationCodeChanged) ||
                                    (sectionDisplayChanged && !categoryIdChanged && !categoryNameChanged && !languageCodeChanged && !publicationCodeChanged && !sectionCodeChanged) ||
                                    (trackDisplayChanged && !categoryIdChanged && !categoryNameChanged && !languageCodeChanged && !publicationCodeChanged && !sectionCodeChanged && !trackCodeChanged);

        var cascadeChangeOccurred = categoryIdChanged || categoryNameChanged || languageCodeChanged || publicationCodeChanged || sectionCodeChanged || trackCodeChanged;

        var changeInfo = new PropertyChangeInfo
        {
            CurrentCategoryId = currentCategoryId,
            CurrentCategoryName = currentCategoryName,
            CurrentLanguageCode = currentLanguageCode,
            CurrentPublicationCode = currentPublicationCode,
            CurrentSectionCode = currentSectionCode,
            CurrentTrackCode = currentTrackCode,
            NewCategoryDisplayText = newCategoryDisplayText,
            NewLanguageDisplayText = newLanguageDisplayText,
            NewPublicationDisplayText = newPublicationDisplayText,
            NewSectionDisplayText = newSectionDisplayText,
            NewTrackDisplayText = newTrackDisplayText,
            NewIsSectionVisible = newIsSectionVisible,
            NewIsLanguageVisible = newIsLanguageVisible,
            NotifyCategory = notifyCategory,
            NotifyLanguage = notifyLanguage,
            NotifyPublication = notifyPublication,
            NotifySection = notifySection,
            NotifyTrack = notifyTrack,
            NotifyIsSectionVisible = notifyIsSectionVisible,
            NotifyIsLanguageVisible = notifyIsLanguageVisible,
            DisplayTextOnlyChanged = displayTextOnlyChanged,
            CascadeChangeOccurred = cascadeChangeOccurred,
            CategoryDisplayChanged = categoryDisplayChanged,
            LanguageDisplayChanged = languageDisplayChanged,
            PublicationDisplayChanged = publicationDisplayChanged,
            SectionDisplayChanged = sectionDisplayChanged,
            TrackDisplayChanged = trackDisplayChanged,
            HasChanges = notifyCategory || notifyLanguage || notifyPublication || notifySection || notifyTrack || notifyIsSectionVisible || notifyIsLanguageVisible || displayTextOnlyChanged
        };

        // Update last values
        lastCategoryId = currentCategoryId;
        lastCategoryName = currentCategoryName;
        lastLanguageCode = currentLanguageCode;
        lastPublicationCode = currentPublicationCode;
        lastSectionCode = currentSectionCode;
        lastTrackCode = currentTrackCode;
        lastCategoryDisplayText = newCategoryDisplayText;
        lastLanguageDisplayText = newLanguageDisplayText;
        lastPublicationDisplayText = newPublicationDisplayText;
        lastSectionDisplayText = newSectionDisplayText;
        lastTrackDisplayText = newTrackDisplayText;
        lastIsSectionVisible = newIsSectionVisible;
        lastIsLanguageVisible = newIsLanguageVisible;

        return changeInfo;
    }

    public record PropertyChangeInfo
    {
        public int? CurrentCategoryId { get; init; }
        public string? CurrentCategoryName { get; init; }
        public string? CurrentLanguageCode { get; init; }
        public string? CurrentPublicationCode { get; init; }
        public string? CurrentSectionCode { get; init; }
        public string? CurrentTrackCode { get; init; }
        public string NewCategoryDisplayText { get; init; } = string.Empty;
        public string NewLanguageDisplayText { get; init; } = string.Empty;
        public string NewPublicationDisplayText { get; init; } = string.Empty;
        public string NewSectionDisplayText { get; init; } = string.Empty;
        public string NewTrackDisplayText { get; init; } = string.Empty;
        public bool NewIsSectionVisible { get; init; }
        public bool NewIsLanguageVisible { get; init; }
        public bool NotifyCategory { get; init; }
        public bool NotifyLanguage { get; init; }
        public bool NotifyPublication { get; init; }
        public bool NotifySection { get; init; }
        public bool NotifyTrack { get; init; }
        public bool NotifyIsSectionVisible { get; init; }
        public bool NotifyIsLanguageVisible { get; init; }
        public bool DisplayTextOnlyChanged { get; init; }
        public bool CascadeChangeOccurred { get; init; }
        public bool CategoryDisplayChanged { get; init; }
        public bool LanguageDisplayChanged { get; init; }
        public bool PublicationDisplayChanged { get; init; }
        public bool SectionDisplayChanged { get; init; }
        public bool TrackDisplayChanged { get; init; }
        public bool HasChanges { get; init; }
    }
}

