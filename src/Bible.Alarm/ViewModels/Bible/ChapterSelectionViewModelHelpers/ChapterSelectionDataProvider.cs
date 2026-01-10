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
        int sectionNumber,
        BibleReadingSchedule? current,
        ObservableCollection<BibleChapterListViewItemModel> chapters,
        Action<BibleChapterListViewItemModel?> setSelectedChapter)
    {
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (chapterViewModelList, selectedChapter) = await Task.Run(async () =>
        {
            var chaptersFromDb = await mediaService.GetBibleChapters(languageCode, publicationCode, sectionNumber);
            var vms = new List<BibleChapterListViewItemModel>();
            BibleChapterListViewItemModel? selected = null;

            foreach (var chapter in chaptersFromDb.Values)
            {
                var chapterVm = new BibleChapterListViewItemModel(chapter);
                vms.Add(chapterVm);

                if (current != null && current.ChapterNumber == chapter.Number)
                {
                    selected = chapterVm;
                    selected.IsSelected = true;
                }
            }

            return (vms, selected);
        });

        // Minimal UI thread work - just swap the collection contents
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

