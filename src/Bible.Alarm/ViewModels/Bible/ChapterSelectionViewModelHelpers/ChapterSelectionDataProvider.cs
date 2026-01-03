#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Serilog;

namespace Bible.Alarm.ViewModels.Bible.ChapterSelectionViewModelHelpers;

/// <summary>
/// Handles data population for ChapterSelectionViewModel.
/// </summary>
public sealed class ChapterSelectionDataProvider(IMediaService mediaService)
{
    public async Task PopulateChapters(
        string languageCode,
        string publicationCode,
        int bookNumber,
        BibleReadingSchedule? current,
        ObservableCollection<BibleChapterListViewItemModel> chapters,
        Action<BibleChapterListViewItemModel?> setSelectedChapter)
    {
        // Run database operations off UI thread
        var chaptersFromDb = await Task.Run(async () =>
            await mediaService.GetBibleChapters(languageCode, publicationCode, bookNumber));

        // Build the list of chapter view models
        var chapterViewModelList = new List<BibleChapterListViewItemModel>();
        BibleChapterListViewItemModel? selectedChapter = null;

        foreach (var chapter in chaptersFromDb.Select(x => x.Value))
        {
            var chapterVm = new BibleChapterListViewItemModel(chapter);

            chapterViewModelList.Add(chapterVm);

            if (current is null)
            {
                continue;
            }

            if (current.ChapterNumber != chapter.Number)
            {
                continue;
            }

            selectedChapter = chapterVm;
            selectedChapter.IsSelected = true;
        }

        // Assign the complete collection on main thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            chapters.Clear();
            foreach (var chapter in chapterViewModelList)
            {
                chapters.Add(chapter);
            }

            if (selectedChapter is not null)
            {
                setSelectedChapter(selectedChapter);
            }
        });
    }

    public void SetSelectedChapter(
        BibleReadingSchedule? current,
        ObservableCollection<BibleChapterListViewItemModel> chapters,
        BibleChapterListViewItemModel? currentSelectedChapter,
        Action<BibleChapterListViewItemModel?> setSelectedChapter)
    {
        if (current == null || chapters == null || chapters.Count == 0)
        {
            return;
        }

        if (currentSelectedChapter != null)
        {
            currentSelectedChapter.IsSelected = false;
        }

        var chapter = chapters.FirstOrDefault(c => c.Number == current.ChapterNumber);
        if (chapter != null)
        {
            setSelectedChapter(chapter);
            chapter.IsSelected = true;
        }
    }
}

