#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Schedule;

/// <summary>
/// Handles cascading property change notifications for bible properties.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePropertyNotifier
{
    private readonly ObservableObject viewModel;

    public BiblePropertyNotifier(ObservableObject viewModel)
    {
        this.viewModel = viewModel;
    }

    /// <summary>
    /// Notifies all display text properties changed in a single batch.
    /// This reduces UI thread work compared to individual notifications.
    /// </summary>
    public void NotifyAllDisplayTextPropertiesChanged()
    {
        viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.LanguageDisplayText));
        viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.TranslationDisplayText));
        viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.BookDisplayText));
        viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.ChapterDisplayText));
    }

    /// <summary>
    /// Notifies properties based on what changed, using cascading logic.
    /// </summary>
    public void NotifyPropertyChanges(BiblePropertyChangeDetector.PropertyChangeInfo changeInfo)
    {
        if (changeInfo.NotifyLanguage)
        {
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.LanguageDisplayText));
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.TranslationDisplayText));
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.BookDisplayText));
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.ChapterDisplayText));
        }
        else if (changeInfo.NotifyTranslation)
        {
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.TranslationDisplayText));
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.BookDisplayText));
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.ChapterDisplayText));
        }
        else if (changeInfo.NotifyBook)
        {
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.BookDisplayText));
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.ChapterDisplayText));
        }
        else if (changeInfo.NotifyChapter)
        {
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.ChapterDisplayText));
        }
        else if (changeInfo.DisplayTextOnlyChanged)
        {
            NotifyDisplayTextOnlyChanges(changeInfo);
        }
    }

    private void NotifyDisplayTextOnlyChanges(BiblePropertyChangeDetector.PropertyChangeInfo changeInfo)
    {
        if (changeInfo.LanguageDisplayChanged)
        {
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.LanguageDisplayText));
        }
        if (changeInfo.TranslationDisplayChanged)
        {
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.TranslationDisplayText));
        }
        if (changeInfo.BookDisplayChanged)
        {
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.BookDisplayText));
        }
        if (changeInfo.ChapterDisplayChanged)
        {
            viewModel.OnPropertyChanged(nameof(BibleSelectionContainerViewModel.ChapterDisplayText));
        }
    }
}

