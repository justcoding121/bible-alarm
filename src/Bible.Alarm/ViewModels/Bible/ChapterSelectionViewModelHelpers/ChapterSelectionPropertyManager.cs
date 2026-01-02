#nullable enable
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Bible.ChapterSelectionViewModelHelpers;

/// <summary>
/// Handles property management for ChapterSelectionViewModel.
/// </summary>
public sealed class ChapterSelectionPropertyManager : ObservableObject
{
    private bool isBusy = true;
    private ObservableCollection<BibleChapterListViewItemModel>? chapters;
    private BibleChapterListViewItemModel? selectedChapter;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public ObservableCollection<BibleChapterListViewItemModel> Chapters
    {
        get => chapters ??= [];
        set => SetProperty(ref chapters, value);
    }

    public BibleChapterListViewItemModel? SelectedChapter
    {
        get => selectedChapter;
        set => SetProperty(ref selectedChapter, value);
    }
}

