#nullable enable
using Bible;
using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BibleSelection;

/// <summary>
/// Handles cascading property change notifications for bible properties.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePropertyNotifier
{
    private readonly Action<string> onPropertyChanged;

    public BiblePropertyNotifier(Action<string> onPropertyChanged)
    {
        this.onPropertyChanged = onPropertyChanged ?? throw new ArgumentNullException(nameof(onPropertyChanged));
    }

    /// <summary>
    /// Notifies all display text properties changed in a single batch.
    /// This reduces UI thread work compared to individual notifications.
    /// </summary>
    public void NotifyAllDisplayTextPropertiesChanged()
    {
        onPropertyChanged(nameof(BibleSelectionContainerViewModel.BibleTypeDisplayText));
        onPropertyChanged(nameof(BibleSelectionContainerViewModel.IsSectionVisible));
        onPropertyChanged(nameof(BibleSelectionContainerViewModel.LanguageDisplayText));
        onPropertyChanged(nameof(BibleSelectionContainerViewModel.TranslationDisplayText));
        onPropertyChanged(nameof(BibleSelectionContainerViewModel.SectionDisplayText));
        onPropertyChanged(nameof(BibleSelectionContainerViewModel.TrackDisplayText));
    }

    /// <summary>
    /// Notifies properties based on what changed, using cascading logic.
    /// </summary>
    public void NotifyPropertyChanges(BiblePropertyChangeDetector.PropertyChangeInfo changeInfo)
    {
        // BibleType change cascades to all properties
        if (changeInfo.NotifyBibleType)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.BibleTypeDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.IsSectionVisible));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.LanguageDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TranslationDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.SectionDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.NotifyLanguage)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.LanguageDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TranslationDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.SectionDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.NotifyTranslation)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TranslationDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.SectionDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.NotifySection)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.SectionDisplayText));
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.NotifyTrack)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TrackDisplayText));
        }
        else if (changeInfo.DisplayTextOnlyChanged)
        {
            NotifyDisplayTextOnlyChanges(changeInfo);
        }

        // Handle IsSectionVisible separately if it changed but BibleType didn't
        if (changeInfo.NotifyIsSectionVisible && !changeInfo.NotifyBibleType)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.IsSectionVisible));
        }
    }

    private void NotifyDisplayTextOnlyChanges(BiblePropertyChangeDetector.PropertyChangeInfo changeInfo)
    {
        if (changeInfo.BibleTypeDisplayChanged)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.BibleTypeDisplayText));
        }
        if (changeInfo.LanguageDisplayChanged)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.LanguageDisplayText));
        }
        if (changeInfo.TranslationDisplayChanged)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TranslationDisplayText));
        }
        if (changeInfo.SectionDisplayChanged)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.SectionDisplayText));
        }
        if (changeInfo.TrackDisplayChanged)
        {
            onPropertyChanged(nameof(BibleSelectionContainerViewModel.TrackDisplayText));
        }
    }
}

