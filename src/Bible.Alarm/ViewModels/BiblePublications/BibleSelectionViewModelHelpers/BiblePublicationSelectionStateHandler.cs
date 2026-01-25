#nullable enable
using System.Collections.ObjectModel;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles state changes and initialization for bible selection.
/// </summary>
public sealed class BiblePublicationSelectionStateHandler
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IMapper mapper;
    private readonly BiblePublicationSelectionDataProvider dataProvider;
    private readonly IServiceScopeFactory scopeFactory;

    // Track last language code and category to detect changes
    private string? lastLanguageCode;
    private string? lastCategoryName;
    private BiblePublicationSchedule? current;
    private BiblePublicationSchedule? lastCurrent;
    private bool initComplete;

    public BiblePublicationSelectionStateHandler(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IMapper mapper,
        BiblePublicationSelectionDataProvider dataProvider,
        IServiceScopeFactory scopeFactory)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.mapper = mapper;
        this.dataProvider = dataProvider;
        this.scopeFactory = scopeFactory;
    }

    public void InitializeCurrent(BiblePublicationSchedule? initialCurrent, string? initialLanguageCode, string? initialCategoryName = null)
    {
        current = initialCurrent;
        lastLanguageCode = initialLanguageCode;
        lastCategoryName = initialCategoryName;
    }

    public async Task HandleBiblePublicationInitializedAsync(Action<bool> setIsBusy, ObservableCollection<LanguageListViewItemModel>? languages, string? languageCode, Action? updateCurrentLanguage = null)
    {
        var stateValue = state.Value;

        try
        {
            // Use CurrentSchedule as the source of truth, not CurrentBiblePublicationSchedule
            string? newLanguageCode = null;
            string? newCategoryName = null;
            if (stateValue.CurrentSchedule != null)
            {
                newLanguageCode = stateValue.CurrentSchedule.BiblePublicationLanguageCode;
                newCategoryName = stateValue.CurrentSchedule.BiblePublicationCategoryName;
            }

            // Check if category changed (need to repopulate languages since they're filtered by category)
            var categoryChanged = newCategoryName != lastCategoryName;

            // If already initialized and languages are populated AND category hasn't changed, ensure IsBusy is false and skip
            if (initComplete && languages != null && languages.Count > 0 && !categoryChanged)
            {
                await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
                return;
            }

            // Update tracking variables
            if (!string.IsNullOrEmpty(newLanguageCode))
            {
                lastLanguageCode = newLanguageCode;
            }
            if (newCategoryName != null)
            {
                lastCategoryName = newCategoryName;
            }

            // Derive from CurrentSchedule (single source of truth)
            var currentSchedule = stateValue.CurrentSchedule;
            if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
            {
                // Create BiblePublicationSchedule from CurrentSchedule
                current = new BiblePublicationSchedule
                {
                    LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                    PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                    SectionCode = currentSchedule.BiblePublicationSectionNumber.HasValue ? currentSchedule.BiblePublicationSectionNumber.Value.ToString() : null,
                    TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 0,
                    FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
                };
                if (string.IsNullOrEmpty(lastLanguageCode))
                {
                    lastLanguageCode = current.LanguageCode;
                }
            }
            else if (currentSchedule != null && !string.IsNullOrEmpty(newLanguageCode))
            {
                // Create a minimal BiblePublicationSchedule from CurrentSchedule
                current = new BiblePublicationSchedule
                {
                    LanguageCode = newLanguageCode,
                    PublicationCode = currentSchedule?.BiblePublicationCode ?? string.Empty,
                    SectionCode = (currentSchedule?.BiblePublicationSectionNumber ?? 1).ToString(),
                    TrackNumber = currentSchedule?.BiblePublicationTrackNumber ?? 1
                };
            }

            // Check if languages are already populated before setting IsBusy to true
            // This avoids unnecessary busy overlay toggling when languages are already loaded
            // Also repopulate if category changed (languages are filtered by category)
            var needsLanguagePopulation = languages == null || languages.Count == 0 || categoryChanged;

            // Only set IsBusy to true if we actually need to populate languages
            // This prevents the quick show/hide toggle when languages are already populated
            if (needsLanguagePopulation)
            {
                await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
            }

            if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            {
                // Initialize with current language code if we have a current schedule
                // Note: publications collection is not available here, will be populated in RefreshFromStateAsync
                await InitializeAsync(current.LanguageCode, languages, null);
            }
            else
            {
                // For language modal use case, just populate languages without publications
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
                            SectionCode = "1",
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
        catch (Exception ex)
        {
            // Log the exception for debugging
            Serilog.Log.Warning(ex, "Error in HandleBiblePublicationInitializedAsync");
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
            // Don't set initComplete on error so it can retry
            // Don't re-throw - async void methods can't properly handle exceptions
        }
    }

    public async Task HandleBiblePublicationChangedAsync(Action<bool> setIsBusy, Action setSelectedPublication, ObservableCollection<PublicationListViewItemModel>? publications)
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentBiblePublicationSchedule
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        var newCategoryName = currentSchedule.BiblePublicationCategoryName;

        if (string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Ensure we always have a valid category - never null or "all"
        // A category should always be selected - if it's null, use last known category as fallback
        if (string.IsNullOrWhiteSpace(newCategoryName))
        {
            newCategoryName = lastCategoryName;
        }

        // If still null after fallback, this is an error condition
        if (string.IsNullOrWhiteSpace(newCategoryName))
        {
            Log.Error("HandleBiblePublicationChangedAsync: Category is null or empty. Category must always be selected. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                newLanguageCode, currentSchedule.BiblePublicationCode);
            throw new InvalidOperationException($"Category must always be selected. No category found in state. LanguageCode={newLanguageCode}, PublicationCode={currentSchedule.BiblePublicationCode}");
        }

        // Check if language code or category changed (need to repopulate publications)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var categoryChanged = newCategoryName != lastCategoryName;

        // If no changes detected and we're already initialized, skip
        if (!languageChanged && !categoryChanged && initComplete)
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        if (newCategoryName != null)
        {
            lastCategoryName = newCategoryName;
        }

        // Derive from CurrentSchedule (single source of truth)
        // currentSchedule is already declared above
        if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
        {
            // Create BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = currentSchedule.BiblePublicationSectionNumber.HasValue ? currentSchedule.BiblePublicationSectionNumber.Value.ToString() : null,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 0,
                FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
            };
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule?.BiblePublicationCode ?? string.Empty,
                SectionCode = (currentSchedule?.BiblePublicationSectionNumber ?? 1).ToString(),
                TrackNumber = currentSchedule?.BiblePublicationTrackNumber ?? 1
            };
            lastCurrent = current;
        }

        // If language or category changed, repopulate publications
        if ((languageChanged || categoryChanged) && initComplete)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
                    // Clear the mapping dictionary before repopulating
                    dataProvider.ClearPublicationVMsMapping();
                    // Pass languageChanged flag to PopulatePublications so it can select default publication
                    // When language changes, don't download all publications yet (only first publication in cascade)
                    // Pass the category name explicitly to ensure correct filtering
                await dataProvider.PopulatePublicationsAsync(newLanguageCode, publications, languageChanged || categoryChanged, downloadAll: false, newCategoryName);
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
            // Update selected publication when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(setSelectedPublication);
        }
    }

    public async Task RefreshFromStateAsync(Action<bool> setIsBusy, ObservableCollection<PublicationListViewItemModel>? publications)
    {
        // Wait for state to be updated (in case language was just changed)
        // This handles the race condition where the modal opens before state is fully updated
        // Use CurrentSchedule as primary source, but fall back to CurrentBiblePublicationSchedule if CurrentSchedule isn't updated yet
        const int maxWaitAttempts = 10;
        const int delayMs = 100;
        string? newLanguageCode = null;
        string? newCategoryName = null;

        for (int i = 0; i < maxWaitAttempts; i++)
        {
            var stateValue = state.Value;

            // Use CurrentSchedule as the source of truth
            if (stateValue.CurrentSchedule != null)
            {
                newLanguageCode = stateValue.CurrentSchedule.BiblePublicationLanguageCode;
                newCategoryName = stateValue.CurrentSchedule.BiblePublicationCategoryName;
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

        // Use category from final state, but fall back to category from wait loop, then last known category
        // This ensures we always filter by the correct category even if state is temporarily null
        // Priority: finalState > waitLoop > lastKnown > database lookup
        var finalCategoryName = currentSchedule.BiblePublicationCategoryName;
        newCategoryName = finalCategoryName ?? newCategoryName ?? lastCategoryName;

        // If still null, try to get category from the publication code in the database
        if (string.IsNullOrWhiteSpace(newCategoryName) && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                
                // Try to get category from BiblePublications first (for downloaded publications)
                var publication = await db.BiblePublications
                    .AsNoTracking()
                    .Include(bp => bp.Category)
                    .Where(bp => bp.PublicationCode == currentSchedule.BiblePublicationCode)
                    .FirstOrDefaultAsync();
                
                if (publication?.Category != null)
                {
                    newCategoryName = publication.Category.CategoryName;
                    Log.Debug("RefreshFromStateAsync: Got category={CategoryName} from BiblePublications for publication={PublicationCode}",
                        newCategoryName, currentSchedule.BiblePublicationCode);
                }
                else
                {
                    // Try PublicationLanguages (for publications not yet downloaded)
                    var publicationLanguage = await db.PublicationLanguages
                        .AsNoTracking()
                        .Include(pl => pl.Category)
                        .Where(pl => pl.PublicationCode == currentSchedule.BiblePublicationCode)
                        .FirstOrDefaultAsync();
                    
                    if (publicationLanguage?.Category != null)
                    {
                        newCategoryName = publicationLanguage.Category.CategoryName;
                        Log.Debug("RefreshFromStateAsync: Got category={CategoryName} from PublicationLanguages for publication={PublicationCode}",
                            newCategoryName, currentSchedule.BiblePublicationCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "RefreshFromStateAsync: Failed to get category from database for publication={PublicationCode}",
                    currentSchedule.BiblePublicationCode);
            }
        }

        // Ensure we always have a valid category - never null or "all"
        // A category should always be selected - if it's still null after all fallbacks, this is an error condition
        if (string.IsNullOrWhiteSpace(newCategoryName))
        {
            Log.Error("RefreshFromStateAsync: Category is null or empty after all fallbacks. Category must always be selected. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                newLanguageCode, currentSchedule.BiblePublicationCode);
            throw new InvalidOperationException($"Category must always be selected. No category found in state or database. LanguageCode={newLanguageCode}, PublicationCode={currentSchedule.BiblePublicationCode}");
        }

        // Check if language code or category changed (need to repopulate publications)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var categoryChanged = newCategoryName != lastCategoryName;

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        // Always update lastCategoryName - a category should always be selected
        lastCategoryName = newCategoryName;

        // Update current from CurrentSchedule (single source of truth)
        if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
        {
            // Create BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = currentSchedule.BiblePublicationSectionNumber.HasValue ? currentSchedule.BiblePublicationSectionNumber.Value.ToString() : null,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 0,
                FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
            };
        }
        else if (!string.IsNullOrEmpty(newLanguageCode))
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule?.BiblePublicationCode ?? string.Empty,
                SectionCode = (currentSchedule?.BiblePublicationSectionNumber ?? 1).ToString(),
                TrackNumber = currentSchedule?.BiblePublicationTrackNumber ?? 1
            };
        }

        // Always repopulate publications if:
        // 1. Not initialized yet
        // 2. Publications collection is null or empty
        // 3. Language code changed (cascade effect)
        // 4. Category changed (cascade effect - filter needs to be reapplied)
        if (!initComplete || publications == null || publications.Count == 0 || languageChanged || categoryChanged)
        {
            initComplete = true;
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
            if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            {
                // Clear the mapping dictionary before repopulating if language or category changed
                if (languageChanged || categoryChanged)
                {
                    dataProvider.ClearPublicationVMsMapping();
                }
                // When publication modal opens, download all publications with first sections and tracks
                // Pass the category name explicitly to ensure correct filtering
                await dataProvider.PopulatePublicationsAsync(current.LanguageCode, publications, languageChanged || categoryChanged, downloadAll: true, newCategoryName);
            }
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
        }
    }

    public BiblePublicationSchedule? Current => current;

    private async Task InitializeAsync(string languageCode, ObservableCollection<LanguageListViewItemModel>? languages, ObservableCollection<PublicationListViewItemModel>? publications)
    {
        await dataProvider.PopulateLanguagesAsync(null, languages);
        await dataProvider.PopulatePublicationsAsync(languageCode, publications, false);
    }
}
