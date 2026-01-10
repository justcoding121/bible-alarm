#nullable enable

using System.Collections.ObjectModel;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Microsoft.Maui.Essentials;
using Serilog;

namespace Bible.Alarm.ViewModels.Bible.SectionSelectionViewModelHelpers;

/// <summary>
/// Handles state change logic for SectionSelectionViewModel.
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
    private readonly Func<ObservableCollection<BibleSectionListViewItemModel>?> getSections;
    private readonly Action<string, string> initialize;
    private readonly Action setSelectedSection;

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
        Func<ObservableCollection<BibleSectionListViewItemModel>?> getSections,
        Action<string, string> initialize,
        Action setSelectedSection)
    {
        this.logger = logger;
        this.mapper = mapper;
        this.getCurrent = getCurrent;
        this.setCurrent = setCurrent;
        this.setLastCurrent = setLastCurrent;
        this.getInitComplete = getInitComplete;
        this.setIsBusy = setIsBusy;
        this.getSections = getSections;
        this.initialize = initialize;
        this.setSelectedSection = setSelectedSection;
    }

    public void HandleStateChanged(ApplicationState stateValue)
    {
        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;
        var newPublicationCode = currentSchedule.BibleReadingPublicationCode;

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        // Check if language or publication code changed (need to repopulate sections)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = languageChanged || publicationCodeChanged;

        // If no changes detected and we're already initialized, skip
        if (!needsRepopulation && getInitComplete())
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        // Update current if we have CurrentBibleReadingSchedule (for other properties like SectionNumber)
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
                SectionNumber = currentSchedule.BibleReadingSectionNumber ?? 1,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
            setCurrent(newCurrent);
            setLastCurrent(newCurrent);
        }

        // If language or publication code changed, repopulate sections
        if (needsRepopulation && getInitComplete())
        {
            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
                    initialize(newLanguageCode, newPublicationCode);
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "SectionSelectionViewModel: OnBibleReadingChanged - Error during repopulation");
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
                }
            });
        }
        else
        {
            // Update selected section when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(setSelectedSection);
        }
    }
}

