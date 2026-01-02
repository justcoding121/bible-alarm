#nullable enable

using System.Collections.ObjectModel;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Microsoft.Maui.Essentials;
using Serilog;

namespace Bible.Alarm.ViewModels.Bible.BookSelectionViewModelHelpers;

/// <summary>
/// Handles state change logic for BookSelectionViewModel.
/// </summary>
public class StateChangeHandler
{
    private readonly ILogger logger;
    private readonly IMapper mapper;
    private readonly Func<BibleReadingSchedule?> getCurrent;
    private readonly Action<BibleReadingSchedule> setCurrent;
    private readonly Action<BibleReadingSchedule> setLastCurrent;
    private readonly Func<bool> getInitComplete;
    private readonly Action<bool> setIsBusy;
    private readonly Func<ObservableCollection<BibleBookListViewItemModel>?> getBooks;
    private readonly Action<string, string> initialize;
    private readonly Action setSelectedBook;

    // Track last language and publication code to detect changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;

    public StateChangeHandler(
        ILogger logger,
        IMapper mapper,
        Func<BibleReadingSchedule?> getCurrent,
        Action<BibleReadingSchedule> setCurrent,
        Action<BibleReadingSchedule> setLastCurrent,
        Func<bool> getInitComplete,
        Action<bool> setIsBusy,
        Func<ObservableCollection<BibleBookListViewItemModel>?> getBooks,
        Action<string, string> initialize,
        Action setSelectedBook)
    {
        this.logger = logger;
        this.mapper = mapper;
        this.getCurrent = getCurrent;
        this.setCurrent = setCurrent;
        this.setLastCurrent = setLastCurrent;
        this.getInitComplete = getInitComplete;
        this.setIsBusy = setIsBusy;
        this.getBooks = getBooks;
        this.initialize = initialize;
        this.setSelectedBook = setSelectedBook;
    }

    public void HandleStateChanged(ApplicationState stateValue)
    {
        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            logger.Warning("BookSelectionViewModel: OnBibleReadingChanged - CurrentSchedule is null, returning");
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;
        var newPublicationCode = currentSchedule.BibleReadingPublicationCode;

        logger.Information("BookSelectionViewModel: OnBibleReadingChanged - CurrentSchedule: Id={ScheduleId}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, InitComplete: {InitComplete}",
            currentSchedule.Id,
            newLanguageCode ?? "null",
            newPublicationCode ?? "null",
            getInitComplete());

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            logger.Warning("BookSelectionViewModel: OnBibleReadingChanged - LanguageCode or PublicationCode is null/empty, returning");
            return;
        }

        // Check if language or publication code changed (need to repopulate books)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = languageChanged || publicationCodeChanged;

        logger.Information("BookSelectionViewModel: OnBibleReadingChanged - LanguageChanged: {LanguageChanged} ({LastLang} -> {NewLang}), PublicationChanged: {PublicationChanged} ({LastPub} -> {NewPub}), NeedsRepopulation: {NeedsRepopulation}",
            languageChanged, lastLanguageCode ?? "null", newLanguageCode,
            publicationCodeChanged, lastPublicationCode ?? "null", newPublicationCode,
            needsRepopulation);

        // If no changes detected and we're already initialized, skip
        if (!needsRepopulation && getInitComplete())
        {
            logger.Debug("BookSelectionViewModel: OnBibleReadingChanged - No changes detected, returning");
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        // Update current if we have CurrentBibleReadingSchedule (for other properties like BookNumber)
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            var newCurrent = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
            setCurrent(newCurrent);
            setLastCurrent(newCurrent);
        }
        else
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            var newCurrent = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                BookNumber = currentSchedule.BibleReadingBookNumber ?? 1,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
            setCurrent(newCurrent);
            setLastCurrent(newCurrent);
        }

        // If language or publication code changed, repopulate books
        if (needsRepopulation && getInitComplete())
        {
            logger.Information("BookSelectionViewModel: OnBibleReadingChanged - Starting repopulation with LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
                newLanguageCode, newPublicationCode);

            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
                    initialize(newLanguageCode, newPublicationCode);
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));

                    logger.Information("BookSelectionViewModel: OnBibleReadingChanged - Repopulation completed. Books count: {BooksCount}",
                        getBooks()?.Count ?? 0);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "BookSelectionViewModel: OnBibleReadingChanged - Error during repopulation");
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
                }
            });
        }
        else
        {
            logger.Information("BookSelectionViewModel: OnBibleReadingChanged - No repopulation needed, updating selected book");
            // Update selected book when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(setSelectedBook);
        }
    }
}

