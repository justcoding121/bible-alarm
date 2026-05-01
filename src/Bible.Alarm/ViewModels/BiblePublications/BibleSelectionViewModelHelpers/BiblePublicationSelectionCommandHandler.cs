#nullable enable
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for bible selection operations.
/// </summary>
public sealed class BiblePublicationSelectionCommandHandler
{
    private const string LogNullPlaceholder = "(null)";

    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;

    public BiblePublicationSelectionCommandHandler(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IBiblePublicationService? biblePublicationService = null,
        ILanguageContentService? languageContentService = null)
    {
        this.mediaService = mediaService;
        this.biblePublicationService = biblePublicationService;
        this.languageContentService = languageContentService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
    }

    public ICommand CreateSectionSelectionCommand(SectionSelectionSelectors selectors, SectionSelectionUiBindings _)
    {
        return new AsyncRelayCommand<PublicationListViewItemModel>(
            async x => await ExecutePublicationSectionSelectionAsync(x, selectors));
    }

    public ICommand CreateBackCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });
    }

    public ICommand CreateCloseModalCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });
    }

    public ICommand CreateSelectLanguageCommand(
        Func<ObservableCollection<LanguageListViewItemModel>> getLanguages,
        Func<Dictionary<string, PublicationListViewItemModel>> getPublicationVMsMapping,
        Action<LanguageListViewItemModel> updateSelectedLanguage,
        Action<bool> setShowProgress,
        Action<double> setProgressPercent,
        Action<string> setProgressText,
        Action<bool> setIsBusy)
    {
        return new AsyncRelayCommand<LanguageListViewItemModel>(
            async x => await ExecuteLanguageModalSelectionAsync(
                x,
                updateSelectedLanguage));
    }

    private async Task ExecutePublicationSectionSelectionAsync(
        PublicationListViewItemModel? x,
        SectionSelectionSelectors selectors)
    {
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionStarting,
            x?.Code ?? LogNullPlaceholder, biblePublicationService != null);

        if (x == null)
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionPublicationNullReturning);
            return;
        }

        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionCurrentScheduleNullReturning);
            return;
        }

        var languageCode = ResolveLanguageCodeForSectionPublicationTap(x, currentSchedule, selectors);
        var currentLanguage = await BuildLanguageModelForSectionFlowAsync(languageCode);

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionCallingGetSectionAndTrack,
            x.Code, currentLanguage.Code);

        var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
        var biblePublicationSectionService = ServiceProviderManager.GetService<IBiblePublicationSectionService>();
        var itemSelector = new BiblePublicationSelectionItemSelector(
            mediaService, state, biblePublicationService, biblePublicationSectionService, languageContentService, scopeFactory);

        var progressReporter = new Bible.Alarm.Common.Helpers.ListItemFetchProgressReporter("BiblePublication", x.Code);

        (string? sectionCode, string trackCode, string sectionName, string trackTitle) result;
        try
        {
            result = await itemSelector.GetSectionAndTrackForPublicationAsync(x, currentLanguage, progressReporter);
        }
        catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
        {
            Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionNetworkErrorForPublication, x.Code);
            await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
            var toastService = ServiceProviderManager.GetService<IToastService>();
            await Task.Delay(500);
            await navigationService.PopModalAsync();
            await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
            return;
        }

        var (sectionCode, trackCode, sectionName, trackTitle) = result;

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionResult,
            sectionCode, trackCode, sectionName, trackTitle);

        if (string.IsNullOrWhiteSpace(trackCode))
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionInvalidTrackCodeReturning, trackCode ?? LogNullPlaceholder);
            WeakReferenceMessenger.Default.Send(new ShowToastMessage("This content is not available at the moment"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(sectionCode) && string.IsNullOrWhiteSpace(sectionName))
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionSectionNameEmptyMayCauseEmptySectionRow,
                sectionCode, x.Code);
        }

        if (string.IsNullOrWhiteSpace(trackTitle))
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionTrackTitleEmptyMayCauseEmptyTrackRow,
                trackCode, x.Code);
        }

        var biblePublicationItem = CreateBiblePublicationItemFromSelection(x, sectionCode, trackCode, sectionName, trackTitle, currentLanguage, currentSchedule);

        Log.Information(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionDispatchingSelection,
            x.Code, sectionCode ?? LogNullPlaceholder, trackCode, sectionName, trackTitle);

        var actionDispatcher = new BiblePublicationSelectionActionDispatcher(dispatcher);
        actionDispatcher.DispatchBiblePublicationSelectionActions(biblePublicationItem);

        await WaitForBiblePublicationTrackCascadeAsync(x.Code);

        await navigationService.PopModalAsync();
    }

    private async Task ExecuteLanguageModalSelectionAsync(
        LanguageListViewItemModel? x,
        Action<LanguageListViewItemModel> updateSelectedLanguage)
    {
        if (x == null)
        {
            return;
        }

        var fetchOccurred = false;
        try
        {
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
            var biblePublicationSectionService = ServiceProviderManager.GetService<IBiblePublicationSectionService>();
            var itemSelector = new BiblePublicationSelectionItemSelector(
                mediaService, state, biblePublicationService, biblePublicationSectionService, languageContentService, scopeFactory);

            var progressReporter = new Bible.Alarm.Common.Helpers.ListItemFetchProgressReporter(
                "BibleLanguage", x.Code, () => fetchOccurred = true);

            (string? publicationCode, string? sectionCode, string trackCode, string sectionName, string publicationName, string trackTitle) result;
            try
            {
                result = await itemSelector.GetPublicationSectionAndTrackForLanguageAsync(x, progressReporter);
            }
            catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
            {
                Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageNetworkErrorDuringSelection, x.Code);
                await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                var toastService = ServiceProviderManager.GetService<IToastService>();
                await Task.Delay(500);
                await navigationService.PopModalAsync();
                await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
                return;
            }

            var (publicationCode, sectionCode, trackCode, sectionName, publicationName, trackTitle) = result;

            if (string.IsNullOrEmpty(publicationCode))
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageNoPublicationsFoundForLanguage, x.Code);
                await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                return;
            }

            if (string.IsNullOrWhiteSpace(trackCode))
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageInvalidTrackForLanguage,
                    trackCode ?? LogNullPlaceholder, x.Code);
                await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                return;
            }

            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageCurrentScheduleNull);
                await MainThread.InvokeOnMainThreadAsync(() => x.DownloadProgress = 0.0);
                return;
            }

            Log.Information(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageCreatingItem,
                x.Code, publicationCode, sectionCode, trackCode);

            updateSelectedLanguage(x);

            var biblePublicationItem = CreateBiblePublicationItemForLanguageSelection(
                x, publicationCode, sectionCode, trackCode, sectionName, publicationName, trackTitle);
            var actionDispatcher = new BiblePublicationSelectionActionDispatcher(dispatcher);
            actionDispatcher.DispatchLanguageSelectionActions(biblePublicationItem);

            await WaitForBibleLanguageCascadeAsync(x.Code);
        }
        finally
        {
            if (fetchOccurred)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    x.DownloadProgress = 1.0;
                });
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    x.DownloadProgress = -1.0;
                });
            }
        }

        await navigationService.PopModalAsync();
    }

    private static string ResolveLanguageCodeForSectionPublicationTap(
        PublicationListViewItemModel publication,
        ScheduleStateItem currentSchedule,
        SectionSelectionSelectors selectors)
    {
        return publication.IsPublicationWithoutLanguage
            ? (currentSchedule.BiblePublicationLanguageCode ?? AppConstants.Media.DefaultLanguageCode)
            : (currentSchedule.BiblePublicationLanguageCode ??
               selectors.GetCurrentLanguage()?.Code ??
               publication.PublicationLanguageCode ??
               AppConstants.Media.DefaultLanguageCode);
    }

    private async Task<LanguageListViewItemModel> BuildLanguageModelForSectionFlowAsync(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return new LanguageListViewItemModel(new Language
            {
                Id = 0,
                LanguageCode = string.Empty,
                Direction = AppConstants.Media.TextDirectionLeftToRight
            }, string.Empty);
        }

        var languageNameService = ServiceProviderManager.GetService<ILanguageNameService>();
        var languages = await mediaService.GetBiblePublicationLanguages();
        if (languages.TryGetValue(languageCode, out var language))
        {
            var name = languageNameService != null
                ? await languageNameService.GetNameAsync(language.Id, AppConstants.Media.DefaultLanguageCode) ?? languageCode
                : languageCode;
            return new LanguageListViewItemModel(language, name);
        }

        return new LanguageListViewItemModel(new Language
        {
            Id = 0,
            LanguageCode = languageCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight
        }, languageCode);
    }

    private async Task WaitForBiblePublicationTrackCascadeAsync(string publicationCode)
    {
        const int maxWaitAttempts = 30;
        const int delayMs = 200;
        for (var i = 0; i < maxWaitAttempts; i++)
        {
            var currentState = state.Value.CurrentSchedule;
            if (currentState != null &&
                currentState.BiblePublicationCode == publicationCode &&
                !string.IsNullOrWhiteSpace(currentState.BiblePublicationTrackCode))
            {
                break;
            }

            await Task.Delay(delayMs);
        }
    }

    private async Task WaitForBibleLanguageCascadeAsync(string languageCode)
    {
        const int maxWaitAttempts = 30;
        const int delayMs = 200;
        for (var i = 0; i < maxWaitAttempts; i++)
        {
            var currentState = state.Value.CurrentSchedule;
            if (currentState != null &&
                currentState.BiblePublicationLanguageCode == languageCode &&
                !string.IsNullOrEmpty(currentState.BiblePublicationCode) &&
                !string.IsNullOrWhiteSpace(currentState.BiblePublicationTrackCode))
            {
                break;
            }

            await Task.Delay(delayMs);
        }
    }

    private static BiblePublicationStateItem CreateBiblePublicationItemFromSelection(
        PublicationListViewItemModel publication,
        string? sectionCode,
        string trackCode,
        string sectionName,
        string trackTitle,
        LanguageListViewItemModel language,
        ScheduleStateItem currentSchedule)
    {
        // Match the pattern used in BiblePublicationSectionSelectionViewModel and TrackSelectionCommandHandler
        // They don't set Id or AlarmScheduleId - let them default to 0
        // IMPORTANT: Always preserve category from current schedule - category can only be changed via CategorySelectionAction
        // Category should NEVER be null in current schedule - if it is, that's a bug that needs to be fixed at the source
        var categoryId = currentSchedule.BiblePublicationCategoryId;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            // Category is null in current schedule - this should NEVER happen
            // Category can only be changed via CategorySelectionAction and should always be preserved
            Log.Error(AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateBiblePublicationItemCategoryNullBug,
                publication.Code, currentSchedule.Id);
        }
        
        return new BiblePublicationStateItem
        {
            CategoryId = categoryId,
            CategoryName = categoryName,
            PublicationCode = publication.Code,
            LanguageCode = language.Code,
            SectionCode = sectionCode,
            TrackCode = trackCode,
            LanguageName = language.Name,
            LanguageDirection = language.Direction,
            PublicationName = publication.Name,
            SectionName = sectionName,
            TrackTitle = trackTitle
        };
    }

    private static BiblePublicationStateItem CreateBiblePublicationItemForLanguageSelection(
        LanguageListViewItemModel language,
        string publicationCode,
        string? sectionCode,
        string trackCode,
        string sectionName,
        string publicationName,
        string trackTitle)
    {
        // Match the pattern used in BiblePublicationSectionSelectionViewModel and TrackSelectionCommandHandler
        // They don't set Id or AlarmScheduleId - let them default to 0
        return new BiblePublicationStateItem
        {
            LanguageCode = language.Code,
            PublicationCode = publicationCode,
            SectionCode = sectionCode,
            TrackCode = trackCode,
            LanguageName = language.Name,
            LanguageDirection = language.Direction,
            PublicationName = publicationName,
            SectionName = sectionName,
            TrackTitle = trackTitle
        };
    }
}
