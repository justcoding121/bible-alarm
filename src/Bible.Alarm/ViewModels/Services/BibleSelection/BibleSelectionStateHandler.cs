#nullable enable
using System.Collections.ObjectModel;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Services.BibleSelection;

/// <summary>
/// Handles state changes and initialization for bible selection.
/// </summary>
public sealed class BibleSelectionStateHandler
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IMapper mapper;
    private readonly BibleSelectionDataProvider dataProvider;

    // Track last language code to detect changes
    private string? lastLanguageCode;
    private BibleReadingSchedule? current;
    private BibleReadingSchedule? lastCurrent;
    private bool initComplete;

    public BibleSelectionStateHandler(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IMapper mapper,
        BibleSelectionDataProvider dataProvider)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.mapper = mapper;
        this.dataProvider = dataProvider;
    }

    public void InitializeCurrent(BibleReadingSchedule? initialCurrent, string? initialLanguageCode)
    {
        current = initialCurrent;
        lastLanguageCode = initialLanguageCode;
    }

    public async Task HandleBibleReadingInitializedAsync(Action<bool> setIsBusy, string? languageCode)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        string? newLanguageCode = null;
        if (stateValue.CurrentSchedule != null)
        {
            newLanguageCode = stateValue.CurrentSchedule.BibleReadingLanguageCode;
        }

        // Update tracking variable
        if (!string.IsNullOrEmpty(newLanguageCode))
        {
            lastLanguageCode = newLanguageCode;
        }

        // Update current if we have CurrentBibleReadingSchedule (for other properties like PublicationCode)
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
            if (string.IsNullOrEmpty(lastLanguageCode))
            {
                lastLanguageCode = current.LanguageCode;
            }
        }
        else if (stateValue.CurrentSchedule != null && !string.IsNullOrEmpty(newLanguageCode))
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = stateValue.CurrentSchedule.BibleReadingPublicationCode,
                BookNumber = stateValue.CurrentSchedule.BibleReadingBookNumber ?? 1,
                ChapterNumber = stateValue.CurrentSchedule.BibleReadingChapterNumber ?? 1
            };
        }

        initComplete = true;

        await Task.Run(async () =>
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));

                if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
                {
                    // Initialize with current language code if we have a current schedule
                    await InitializeAsync(current.LanguageCode);
                }
                else
                {
                    // For language modal use case, just populate languages without translations
                    await dataProvider.PopulateLanguagesAsync(null, null);

                    // Subscribe to LanguageSearchTerm property changes will be handled by the view model
                }

                // CollectionView needs a moment to render before hiding the busy indicator
                await Task.Delay(100);

                await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
#if DEBUG
                Log.Error(ex, "Error initializing BibleSelectionStateHandler");
#endif
            }
        });
    }

    public async Task HandleBibleReadingChangedAsync(Action<bool> setIsBusy, Action setSelectedTranslation)
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentBibleReadingSchedule
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;

        if (string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Check if language code changed (need to repopulate translations)
        var languageChanged = lastLanguageCode != newLanguageCode;

        // If no changes detected and we're already initialized, skip
        if (!languageChanged && initComplete)
        {
            return;
        }

        // Update tracking variable
        lastLanguageCode = newLanguageCode;

        // Update current if we have CurrentBibleReadingSchedule (for other properties like PublicationCode)
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BibleReadingSchedule from CurrentSchedule
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule.BibleReadingPublicationCode,
                BookNumber = currentSchedule.BibleReadingBookNumber ?? 1,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
            lastCurrent = current;
        }

        // If language changed, repopulate translations
        if (languageChanged && initComplete)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
                    // Clear the mapping dictionary before repopulating
                    dataProvider.ClearTranslationVMsMapping();
                    // Pass languageChanged flag to PopulateTranslations so it can select default translation
                    await dataProvider.PopulateTranslationsAsync(newLanguageCode, null, languageChanged);
                    await Task.Delay(100);
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
                }
                catch (Exception ex)
                {
#if DEBUG
                    Log.Error(ex, "Error repopulating translations in BibleSelectionStateHandler");
#endif
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
                }
            });
        }
        else
        {
            // Update selected translation when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(setSelectedTranslation);
        }
    }

    public async Task RefreshFromStateAsync(Action<bool> setIsBusy, ObservableCollection<PublicationListViewItemModel>? translations)
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BibleReadingLanguageCode;

        if (string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Update tracking variable
        lastLanguageCode = newLanguageCode;

        // Update current from CurrentSchedule
        if (stateValue.CurrentBibleReadingSchedule != null)
        {
            current = mapper.Map<BibleReadingSchedule>(stateValue.CurrentBibleReadingSchedule);
        }
        else if (!string.IsNullOrEmpty(newLanguageCode))
        {
            current = new BibleReadingSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule.BibleReadingPublicationCode,
                BookNumber = currentSchedule.BibleReadingBookNumber ?? 1,
                ChapterNumber = currentSchedule.BibleReadingChapterNumber ?? 1
            };
        }

        // Ensure translations are populated if not already initialized
        if (!initComplete || translations == null || translations.Count == 0)
        {
            initComplete = true;
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
            if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            {
                await dataProvider.PopulateTranslationsAsync(current.LanguageCode, null, false);
            }
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
        }
    }

    public BibleReadingSchedule? Current => current;

    private async Task InitializeAsync(string languageCode)
    {
        await dataProvider.PopulateLanguagesAsync(null, null);
        await dataProvider.PopulateTranslationsAsync(languageCode, null, false);
    }
}
