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
public sealed class BiblePublicationSelectionStateHandler
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IMapper mapper;
    private readonly BiblePublicationSelectionDataProvider dataProvider;

    // Track last language code to detect changes
    private string? lastLanguageCode;
    private BiblePublicationSchedule? current;
    private BiblePublicationSchedule? lastCurrent;
    private bool initComplete;

    public BiblePublicationSelectionStateHandler(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IMapper mapper,
        BiblePublicationSelectionDataProvider dataProvider)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.mapper = mapper;
        this.dataProvider = dataProvider;
    }

    public void InitializeCurrent(BiblePublicationSchedule? initialCurrent, string? initialLanguageCode)
    {
        current = initialCurrent;
        lastLanguageCode = initialLanguageCode;
    }

    public async Task HandleBiblePublicationInitializedAsync(Action<bool> setIsBusy, ObservableCollection<LanguageListViewItemModel>? languages, string? languageCode, Action? updateCurrentLanguage = null)
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
            // Use CurrentSchedule as the source of truth, not CurrentBiblePublicationSchedule
            string? newLanguageCode = null;
            if (stateValue.CurrentSchedule != null)
            {
                newLanguageCode = stateValue.CurrentSchedule.BiblePublicationLanguageCode;
            }

            // Update tracking variable
            if (!string.IsNullOrEmpty(newLanguageCode))
            {
                lastLanguageCode = newLanguageCode;
            }

            // Update current if we have CurrentBiblePublicationSchedule (for other properties like PublicationCode)
            if (stateValue.CurrentBiblePublicationSchedule != null)
            {
                current = mapper.Map<BiblePublicationSchedule>(stateValue.CurrentBiblePublicationSchedule);
                if (string.IsNullOrEmpty(lastLanguageCode))
                {
                    lastLanguageCode = current.LanguageCode;
                }
            }
            else if (stateValue.CurrentSchedule != null && !string.IsNullOrEmpty(newLanguageCode))
            {
                // Create a minimal BiblePublicationSchedule from CurrentSchedule
                current = new BiblePublicationSchedule
                {
                    LanguageCode = newLanguageCode,
                    PublicationCode = stateValue.CurrentSchedule.BiblePublicationCode,
                    SectionNumber = stateValue.CurrentSchedule.BiblePublicationSectionNumber ?? 1,
                    TrackNumber = stateValue.CurrentSchedule.BiblePublicationTrackNumber ?? 1
                };
            }

            // Check if languages are already populated before setting IsBusy to true
            // This avoids unnecessary busy overlay toggling when languages are already loaded
            var needsLanguagePopulation = languages == null || languages.Count == 0;
            
            // Only set IsBusy to true if we actually need to populate languages
            // This prevents the quick show/hide toggle when languages are already populated
            if (needsLanguagePopulation)
            {
                await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
            }

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

            // CollectionView needs a moment to render before hiding the busy indicator (only if we showed it)
            if (needsLanguagePopulation)
            {
                await Task.Delay(100);
            }

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
                        current = new BiblePublicationSchedule
                        {
                            LanguageCode = selectedLanguage.Code,
                            PublicationCode = string.Empty,
                            SectionNumber = 1,
                            TrackNumber = 1
                        };
                    }
                    else
                    {
                        current.LanguageCode = selectedLanguage.Code;
                    }
                }
            }

            // Only set IsBusy to false if we set it to true earlier
            if (needsLanguagePopulation)
            {
                await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
            }
        }
        catch (Exception)
        {
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
            // Don't set initComplete on error so it can retry
            // Don't re-throw - async void methods can't properly handle exceptions
        }
    }

    public async Task HandleBiblePublicationChangedAsync(Action<bool> setIsBusy, Action setSelectedTranslation, ObservableCollection<PublicationListViewItemModel>? translations)
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentBiblePublicationSchedule
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;

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

        // Update current if we have CurrentBiblePublicationSchedule (for other properties like PublicationCode)
        if (stateValue.CurrentBiblePublicationSchedule != null)
        {
            current = mapper.Map<BiblePublicationSchedule>(stateValue.CurrentBiblePublicationSchedule);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode,
                SectionNumber = currentSchedule.BiblePublicationSectionNumber ?? 1,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 1
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
                catch (Exception)
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
        // Wait for state to be updated (in case language was just changed)
        // This handles the race condition where the modal opens before state is fully updated
        // Use CurrentSchedule as primary source, but fall back to CurrentBiblePublicationSchedule if CurrentSchedule isn't updated yet
        const int maxWaitAttempts = 10;
        const int delayMs = 100;
        string? newLanguageCode = null;

        for (int i = 0; i < maxWaitAttempts; i++)
        {
            var stateValue = state.Value;

            // Use CurrentSchedule as the source of truth
            if (stateValue.CurrentSchedule != null)
            {
                newLanguageCode = stateValue.CurrentSchedule.BiblePublicationLanguageCode;
                if (!string.IsNullOrEmpty(newLanguageCode))
                {
                    break;
                }
            }

            // Fall back to CurrentBiblePublicationSchedule if CurrentSchedule isn't updated yet
            // This handles the case where BiblePublicationSelectionAction updates CurrentBiblePublicationSchedule
            // but the effect that syncs to CurrentSchedule hasn't run yet
            if (string.IsNullOrEmpty(newLanguageCode) && stateValue.CurrentBiblePublicationSchedule != null)
            {
                newLanguageCode = stateValue.CurrentBiblePublicationSchedule.LanguageCode;
                if (!string.IsNullOrEmpty(newLanguageCode))
                {
                    break;
                }
            }

            // Wait a bit and retry if language code is not set yet
            await Task.Delay(delayMs);
        }

        // If language code is still null after waiting, return early
        if (string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        var finalStateValue = state.Value;
        var currentSchedule = finalStateValue.CurrentSchedule!;

        // Check if language code changed (need to repopulate translations)
        var languageChanged = lastLanguageCode != newLanguageCode;

        // Update tracking variable
        lastLanguageCode = newLanguageCode;

        // Update current from CurrentSchedule
        if (finalStateValue.CurrentBiblePublicationSchedule != null)
        {
            current = mapper.Map<BiblePublicationSchedule>(finalStateValue.CurrentBiblePublicationSchedule);
        }
        else if (!string.IsNullOrEmpty(newLanguageCode))
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode,
                SectionNumber = currentSchedule.BiblePublicationSectionNumber ?? 1,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 1
            };
        }

        // Always repopulate translations if:
        // 1. Not initialized yet
        // 2. Translations collection is null or empty
        // 3. Language code changed (cascade effect)
        if (!initComplete || translations == null || translations.Count == 0 || languageChanged)
        {
            initComplete = true;
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
            if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            {
                // Clear the mapping dictionary before repopulating if language changed
                if (languageChanged)
                {
                    dataProvider.ClearTranslationVMsMapping();
                }
                await dataProvider.PopulateTranslationsAsync(current.LanguageCode, translations, languageChanged);
            }
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
        }
    }

    public BiblePublicationSchedule? Current => current;

    private async Task InitializeAsync(string languageCode, ObservableCollection<LanguageListViewItemModel>? languages, ObservableCollection<PublicationListViewItemModel>? translations)
    {
        await dataProvider.PopulateLanguagesAsync(null, languages);
        await dataProvider.PopulateTranslationsAsync(languageCode, translations, false);
    }
}
