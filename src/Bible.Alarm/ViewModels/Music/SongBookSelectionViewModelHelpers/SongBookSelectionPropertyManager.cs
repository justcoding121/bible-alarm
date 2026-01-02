#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Music.SongBookSelectionViewModelHelpers;

/// <summary>
/// Handles property management for SongBookSelectionViewModel.
/// </summary>
public sealed class SongBookSelectionPropertyManager : ObservableObject
{
    private bool isBusy = true;
    private ObservableCollection<PublicationListViewItemModel>? songBooks;
    private ObservableCollection<LanguageListViewItemModel>? languages;
    private LanguageListViewItemModel? currentLanguage;
    private string languageSearchTerm = string.Empty;
    private PublicationListViewItemModel? selectedSongBook;
    private PropertyChangedEventHandler? propertyChangedHandler;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public ObservableCollection<PublicationListViewItemModel> SongBooks
    {
        get => songBooks ??= [];
        set => SetProperty(ref songBooks, value);
    }

    public ObservableCollection<LanguageListViewItemModel> Languages
    {
        get => languages ??= [];
        set => SetProperty(ref languages, value);
    }

    public LanguageListViewItemModel? CurrentLanguage
    {
        get => currentLanguage;
        set => SetProperty(ref currentLanguage, value);
    }

    public string LanguageSearchTerm
    {
        get => languageSearchTerm;
        set => SetProperty(ref languageSearchTerm, value);
    }

    public PublicationListViewItemModel? SelectedSongBook
    {
        get => selectedSongBook;
        set => SetProperty(ref selectedSongBook, value);
    }

    public object? SelectedItem => CurrentLanguage;

    public void SetupLanguageSearchHandler(Func<string?, Task> populateLanguages)
    {
        propertyChangedHandler = (sender, e) =>
        {
            if (e.PropertyName == nameof(LanguageSearchTerm))
            {
                _ = populateLanguages(LanguageSearchTerm?.Trim());
            }
        };
        PropertyChanged += propertyChangedHandler;
    }

    public void RemoveLanguageSearchHandler()
    {
        if (propertyChangedHandler != null)
        {
            PropertyChanged -= propertyChangedHandler;
            propertyChangedHandler = null;
        }
    }
}

