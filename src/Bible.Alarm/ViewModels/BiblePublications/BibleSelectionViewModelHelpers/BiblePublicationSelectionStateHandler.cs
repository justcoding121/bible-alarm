#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
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
    private readonly IState<ApplicationState> state;
    private readonly BiblePublicationSelectionDataProvider dataProvider;
    private readonly IServiceScopeFactory scopeFactory;

    // Track last language code and category to detect changes
    private string? lastLanguageCode;
    private string? lastCategoryName;
    private BiblePublicationSchedule? current;
    private bool initComplete;

    public BiblePublicationSelectionStateHandler(
        IState<ApplicationState> state,
        BiblePublicationSelectionDataProvider dataProvider,
        IServiceScopeFactory scopeFactory)
    {
        this.state = state;
        this.dataProvider = dataProvider;
        this.scopeFactory = scopeFactory;
    }

    public void InitializeCurrent(BiblePublicationSchedule? initialCurrent, string? initialLanguageCode, string? initialCategoryName = null)
    {
        current = initialCurrent;
        lastLanguageCode = initialLanguageCode;
        lastCategoryName = initialCategoryName;
    }

    /// <summary>
    /// Initializes Bible publication data. 
    /// NOTE: This method should NOT set IsBusy = false. The modal code-behind (via ModalScrollHelper)
    /// is responsible for clearing IsBusy after the list is rendered.
    /// </summary>
    public async Task HandleBiblePublicationInitializedAsync(Action<bool> setIsBusy, ObservableCollection<LanguageListViewItemModel>? languages, string? languageCode, Action? updateCurrentLanguage = null)
    {
        var stateValue = state.Value;

        try
        {
            ExtractLanguageAndCategoryFromSchedule(stateValue, out var newLanguageCode, out var newCategoryName);

            var categoryChanged = newCategoryName != lastCategoryName;

            if (initComplete && languages != null && languages.Count > 0 && !categoryChanged)
            {
                return;
            }

            if (!string.IsNullOrEmpty(newLanguageCode))
            {
                lastLanguageCode = newLanguageCode;
            }

            if (newCategoryName != null)
            {
                lastCategoryName = newCategoryName;
            }

            var currentSchedule = stateValue.CurrentSchedule;
            UpdateCurrentScheduleModelFromState(currentSchedule, newLanguageCode);

            var needsLanguagePopulation = languages == null || languages.Count == 0 || categoryChanged;

            if (needsLanguagePopulation)
            {
                await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
            }

            if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            {
                await InitializeAsync(current.LanguageCode, languages, null);
            }
            else
            {
                if (languages == null)
                {
                    return;
                }

                await dataProvider.PopulateLanguagesAsync(null, languages);
            }

            updateCurrentLanguage?.Invoke();

            initComplete = true;

            AlignCurrentWithSelectedLanguage(languages);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionStateHandlerDiagnosticsLog.ErrorInHandleBiblePublicationInitializedAsync);
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
            throw new InvalidOperationException(
                $"HandleBiblePublicationChangedAsync: Category is null or empty. Category must always be selected. LanguageCode={newLanguageCode}, PublicationCode={currentSchedule.BiblePublicationCode}");
        }

        // Check if language code or category changed (need to repopulate publications)
        var languageChanged = lastLanguageCode != newLanguageCode;
        var categoryChanged = newCategoryName != lastCategoryName;

        // If no changes detected and we're already initialized, skip
        if (!languageChanged && !categoryChanged && initComplete)
        {
            return;
        }

        lastLanguageCode = newLanguageCode;
        lastCategoryName = newCategoryName;

        // Derive from CurrentSchedule (single source of truth)
        current = new BiblePublicationSchedule
        {
            LanguageCode = newLanguageCode,
            PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
            SectionCode = currentSchedule.BiblePublicationSectionCode,
            TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty,
            FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
        };

        // If language or category changed, repopulate publications
        if ((languageChanged || categoryChanged) && initComplete)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
                    dataProvider.ClearPublicationVMsMapping();
                    await dataProvider.PopulatePublicationsAsync(newLanguageCode, publications, languageChanged || categoryChanged, downloadAll: false, newCategoryName);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionStateHandlerDiagnosticsLog.ErrorInHandleBiblePublicationChangedAsyncDuringPublicationPopulation);
                }
                finally
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

    /// <summary>
    /// Refreshes publication data from state.
    /// NOTE: This method should NOT set IsBusy = false. The modal code-behind (via ModalScrollHelper)
    /// is responsible for clearing IsBusy after the list is rendered.
    /// </summary>
    public async Task RefreshFromStateAsync(Action<bool> setIsBusy, ObservableCollection<PublicationListViewItemModel>? publications, IFetchProgress? progress = null)
    {
        const int maxWaitAttempts = 10;
        const int delayMs = 100;

        var (newLanguageCode, newCategoryName) = await WaitForScheduleLanguageAndCategoryAsync(maxWaitAttempts, delayMs);

        if (string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        var finalStateValue = state.Value;
        var currentSchedule = finalStateValue.CurrentSchedule!;

        newCategoryName = CoalesceCategoryName(currentSchedule.BiblePublicationCategoryName, newCategoryName);

        newCategoryName = await TryAugmentCategoryFromDatabaseAsync(newCategoryName, currentSchedule);

        if (string.IsNullOrWhiteSpace(newCategoryName))
        {
            throw new InvalidOperationException(
                $"RefreshFromStateAsync: Category is null or empty after all fallbacks. Category must always be selected. LanguageCode={newLanguageCode}, PublicationCode={currentSchedule.BiblePublicationCode}");
        }

        var languageChanged = lastLanguageCode != newLanguageCode;
        var categoryChanged = newCategoryName != lastCategoryName;

        lastLanguageCode = newLanguageCode;
        lastCategoryName = newCategoryName;

        RefreshCurrentModelFromSchedule(currentSchedule, newLanguageCode);

        if (!initComplete || publications == null || publications.Count == 0 || languageChanged || categoryChanged)
        {
            initComplete = true;
            await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
            if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            {
                if (languageChanged || categoryChanged)
                {
                    dataProvider.ClearPublicationVMsMapping();
                }

                await dataProvider.PopulatePublicationsAsync(current.LanguageCode, publications, languageChanged || categoryChanged, downloadAll: true, newCategoryName, progress);
            }
        }
    }

    private static void ExtractLanguageAndCategoryFromSchedule(
        ApplicationState stateValue,
        out string? newLanguageCode,
        out string? newCategoryName)
    {
        newLanguageCode = null;
        newCategoryName = null;
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        newLanguageCode = stateValue.CurrentSchedule.BiblePublicationLanguageCode;
        newCategoryName = stateValue.CurrentSchedule.BiblePublicationCategoryName;
    }

    private void UpdateCurrentScheduleModelFromState(ScheduleStateItem? currentSchedule, string? newLanguageCode)
    {
        if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = currentSchedule.BiblePublicationSectionCode,
                TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty,
                FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
            };

            if (string.IsNullOrEmpty(lastLanguageCode))
            {
                lastLanguageCode = current.LanguageCode;
            }
        }
        else if (currentSchedule != null && !string.IsNullOrEmpty(newLanguageCode))
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = currentSchedule.BiblePublicationSectionCode,
                TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty
            };
        }
    }

    private void AlignCurrentWithSelectedLanguage(ObservableCollection<LanguageListViewItemModel>? languages)
    {
        if (languages == null)
        {
            return;
        }

        var selectedLanguage = languages.FirstOrDefault(l => l.IsSelected);
        if (selectedLanguage == null)
        {
            return;
        }

        if (current == null)
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = selectedLanguage.Code,
                PublicationCode = string.Empty,
                SectionCode = "1",
                TrackCode = "1"
            };
        }
        else
        {
            current.LanguageCode = selectedLanguage.Code;
        }
    }

    private async Task<(string? LangCode, string? CategoryName)> WaitForScheduleLanguageAndCategoryAsync(
        int maxWaitAttempts,
        int delayMs)
    {
        string? newLanguageCode = null;
        string? newCategoryName = null;

        for (var i = 0; i < maxWaitAttempts; i++)
        {
            var stateValue = state.Value;

            if (stateValue.CurrentSchedule != null)
            {
                newLanguageCode = stateValue.CurrentSchedule.BiblePublicationLanguageCode;
                newCategoryName = stateValue.CurrentSchedule.BiblePublicationCategoryName;
                if (!string.IsNullOrEmpty(newLanguageCode))
                {
                    break;
                }
            }

            await Task.Delay(delayMs);
        }

        return (newLanguageCode, newCategoryName);
    }

    private string? CoalesceCategoryName(string? fromSchedule, string? fromWaitLoop)
    {
        return fromSchedule ?? fromWaitLoop ?? lastCategoryName;
    }

    private async Task<string?> TryAugmentCategoryFromDatabaseAsync(string? newCategoryName, ScheduleStateItem currentSchedule)
    {
        if (!string.IsNullOrWhiteSpace(newCategoryName) || string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            return newCategoryName;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publication = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bpc => bpc.Category)
                .Where(bp => bp.PublicationCode == currentSchedule.BiblePublicationCode)
                .FirstOrDefaultAsync();

            if (publication?.PrimaryCategory != null)
            {
                Log.Debug(AppConstants.Logging.BiblePublicationSelectionStateHandlerDiagnosticsLog.RefreshFromStateGotCategoryFromBiblePublications,
                    publication.PrimaryCategory.CategoryCode, currentSchedule.BiblePublicationCode);
                return publication.PrimaryCategory.CategoryCode;
            }

            var publicationLanguage = await db.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Category)
                .Where(pl => pl.PublicationCode == currentSchedule.BiblePublicationCode)
                .FirstOrDefaultAsync();

            if (publicationLanguage?.Category != null)
            {
                Log.Debug(AppConstants.Logging.BiblePublicationSelectionStateHandlerDiagnosticsLog.RefreshFromStateGotCategoryFromPublicationLanguages,
                    publicationLanguage.Category.CategoryCode, currentSchedule.BiblePublicationCode);
                return publicationLanguage.Category.CategoryCode;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionStateHandlerDiagnosticsLog.RefreshFromStateFailedToGetCategoryFromDatabase,
                currentSchedule.BiblePublicationCode);
        }

        return newCategoryName;
    }

    private void RefreshCurrentModelFromSchedule(ScheduleStateItem currentSchedule, string newLanguageCode)
    {
        if (!string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = currentSchedule.BiblePublicationSectionCode,
                TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty,
                FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
            };
        }
        else if (!string.IsNullOrEmpty(newLanguageCode))
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = currentSchedule.BiblePublicationSectionCode,
                TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty
            };
        }
    }

    public BiblePublicationSchedule? Current => current;

    private async Task InitializeAsync(string languageCode, ObservableCollection<LanguageListViewItemModel>? languages, ObservableCollection<PublicationListViewItemModel>? publications)
    {
        // Only populate languages if not already populated (avoids duplicate population during modal open)
        if (languages == null || languages.Count == 0)
        {
            await dataProvider.PopulateLanguagesAsync(null, languages);
        }
        
        await dataProvider.PopulatePublicationsAsync(languageCode, publications, false, downloadAll: false, categoryName: null);
    }
}
