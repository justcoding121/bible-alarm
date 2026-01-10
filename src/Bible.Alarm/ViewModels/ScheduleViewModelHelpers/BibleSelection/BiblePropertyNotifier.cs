#nullable enable
using Bible;
using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BibleSelection;

/// <summary>
/// Handles cascading property change notifications for bible properties.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePublicationPropertyNotifier
{
    private readonly Action<string> onPropertyChanged;

    public BiblePublicationPropertyNotifier(Action<string> onPropertyChanged)
    {
        this.onPropertyChanged = onPropertyChanged ?? throw new ArgumentNullException(nameof(onPropertyChanged));
    }

    /// <summary>
    /// Notifies all display text properties changed in a single batch.
    /// This reduces UI thread work compared to individual notifications.
    /// </summary>
    public void NotifyAllDisplayTextPropertiesChanged()
    {
        onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.BibleTypeDisplayText));
        onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.IsSectionVisible));
        onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.LanguageDisplayText));
        onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TranslationDisplayText));
        onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText));
        onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText));
    }

    /// <summary>
    /// Notifies properties based on what changed, using cascading logic.
    /// </summary>
    public void NotifyPropertyChanges(BiblePublicationPropertyChangeDetector.PropertyChangeInfo changeInfo)
    {
        // BibleType change cascades to all properties
        if (changeInfo.NotifyBibleType)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.BibleTypeDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.IsSectionVisible));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.LanguageDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TranslationDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.NotifyLanguage)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.LanguageDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TranslationDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.NotifyTranslation)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TranslationDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.NotifySection)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText));
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.NotifyTrack)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.DisplayTextOnlyChanged)
        {
            NotifyDisplayTextOnlyChanges(changeInfo);
        }

        // Handle IsSectionVisible separately if it changed but BibleType didn't
        if (changeInfo.NotifyIsSectionVisible && !changeInfo.NotifyBibleType)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.IsSectionVisible));
        }
    }

    private void NotifyDisplayTextOnlyChanges(BiblePublicationPropertyChangeDetector.PropertyChangeInfo changeInfo)
    {
        if (changeInfo.BibleTypeDisplayChanged)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.BibleTypeDisplayText));
        }
        if (changeInfo.LanguageDisplayChanged)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.LanguageDisplayText));
        }
        if (changeInfo.TranslationDisplayChanged)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TranslationDisplayText));
        }
        if (changeInfo.SectionDisplayChanged)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText));
        }
        if (changeInfo.TrackDisplayChanged)
        {
            onPropertyChanged(nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText));
        }
    }
}

