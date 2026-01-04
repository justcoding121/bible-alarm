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

namespace Bible.Alarm.ViewModels.Bible.BibleSelectionViewModelHelpers;

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

    public async Task HandleBibleReadingInitializedAsync(Action<bool> setIsBusy, ObservableCollection<LanguageListViewItemModel>? languages, string? languageCode, Action? updateCurrentLanguage = null)
    {
        var stateValue = state.Value;

        // If already initialized and languages are populated, ensure IsBusy is false and skip
        if (initComplete && languages != null && languages.Count > 0)
        {
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
            return;
        }

        try
        {
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

            // Always populate languages, even if initComplete is true but languages aren't populated
            // This handles the case where the modal opens before initialization completes
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));

            if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            {
                // Initialize with current language code if we have a current schedule
                // Note: translations collection is not available here, will be populated in RefreshFromStateAsync
                await InitializeAsync(current.LanguageCode, languages, null);
            }
            else
            {
                // For language modal use case, just populate languages without translations
                // Pass the languages collection so it gets populated and displayed
                // Ensure languages collection is not null - it should be initialized by property manager
                if (languages == null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
                    return;
                }

                await dataProvider.PopulateLanguagesAsync(null, languages);
            }

            // Update CurrentLanguage after languages are populated
            updateCurrentLanguage?.Invoke();

            // CollectionView needs a moment to render before hiding the busy indicator
            await Task.Delay(100);

            // Set initComplete to true only after languages are successfully populated
            initComplete = true;

            // Set CurrentLanguage to the selected language for scrolling to work
            if (languages != null)
            {
                var selectedLanguage = languages.FirstOrDefault(l => l.IsSelected);
                if (selectedLanguage != null)
                {
                    // We need to set the CurrentLanguage property through the property manager
                    // But we don't have direct access to it here. The property manager should be updated
                    // when the state changes, but for the modal, we need to set it explicitly.
                    // For now, let's set current to have the correct language code so SelectedItem works
                    if (current == null)
                    {
                        current = new BibleReadingSchedule
                        {
                            LanguageCode = selectedLanguage.Code,
                            PublicationCode = string.Empty,
                            BookNumber = 1,
                            ChapterNumber = 1
                        };
                    }
                    else
                    {
                        current.LanguageCode = selectedLanguage.Code;
                    }
                }
            }

            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
            // Don't set initComplete on error so it can retry
            // Don't re-throw - async void methods can't properly handle exceptions
        }
    }

    public async Task HandleBibleReadingChangedAsync(Action<bool> setIsBusy, Action setSelectedTranslation, ObservableCollection<PublicationListViewItemModel>? translations)
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
                    await dataProvider.PopulateTranslationsAsync(newLanguageCode, translations, languageChanged);
                    await Task.Delay(100);
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
                }
                catch (Exception ex)
                {
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
                await dataProvider.PopulateTranslationsAsync(current.LanguageCode, translations, false);
            }
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
        }
    }

    public BibleReadingSchedule? Current => current;

    private async Task InitializeAsync(string languageCode, ObservableCollection<LanguageListViewItemModel>? languages, ObservableCollection<PublicationListViewItemModel>? translations)
    {
        await dataProvider.PopulateLanguagesAsync(null, languages);
        await dataProvider.PopulateTranslationsAsync(languageCode, translations, false);
    }
}
