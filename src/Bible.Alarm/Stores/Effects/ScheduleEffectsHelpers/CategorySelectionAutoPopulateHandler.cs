#nullable enable
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.CategorySelectionAutoPopulateHandlerHelpers;
using Bible.Alarm.Stores.Messages.CategoryProgress;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

public sealed class CategorySelectionAutoPopulateHandler
{
    private readonly IBiblePublicationService biblePublicationService;
    private readonly IMediaService mediaService;
    private readonly ILanguageContentService languageContentService;
    private readonly ILanguageNameService languageNameService;
    private readonly BiblePublicationSelectionItemSelector itemSelector;
    private readonly IState<ApplicationState> state;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;

    public CategorySelectionAutoPopulateHandler(CategorySelectionAutoPopulateHandlerDeps d)
    {
        biblePublicationService = d.BiblePublicationService;
        mediaService = d.MediaService;
        languageContentService = d.LanguageContentService;
        languageNameService = d.LanguageNameService;
        itemSelector = d.ItemSelector;
        state = d.State;
        scopeFactory = d.ScopeFactory;
        logger = d.Logger;
    }

    public async Task HandleAsync(CategorySelectionAction action, IDispatcher dispatcher)
    {
        void ReportProgress(double progress, bool isComplete = false) =>
            SendCategoryFetchProgress(action.CategoryId, progress, isComplete);

        var fetchOccurred = false;

        try
        {
            logger.Information(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.StartingAutoPopulation,
                action.CategoryName);

            // Progress will only be reported when fetches actually happen (via progress tracker)
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                logger.Warning(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.CurrentScheduleNullSkippingAutoPopulation);
                return;
            }

            var languages = await biblePublicationService.GetDistinctLanguagesAsync(action.CategoryName);
            var selectedLanguage = SelectLanguageForCategory(action, languages);

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publicationFetchAccumulator = new PublicationCatalogFetchAccumulator();
            var (publicationCode, publicationWithoutLanguage) =
                await ResolvePublicationForCategorySelectionAsync(action, selectedLanguage, db,
                    publicationFetchAccumulator);

            fetchOccurred = publicationFetchAccumulator.FetchOccurred;

            if (string.IsNullOrEmpty(publicationCode))
            {
                var languageCodeForWarning = selectedLanguage?.LanguageCode ?? "N/A";
                logger.Warning(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NoPublicationFoundOrCatalogedForLanguageCategory,
                    languageCodeForWarning, action.CategoryName);
                return;
            }

            logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedPublicationWithoutLanguageFlag,
                publicationCode, publicationWithoutLanguage);

            var trackBundle = await TryResolvePrimaryTrackBundleAsync(publicationWithoutLanguage, selectedLanguage, publicationCode,
                action.CategoryName);

            if (trackBundle is not { } bundle)
            {
                return;
            }

            publicationCode = bundle.PublicationCode;
            var sectionCode = bundle.SectionCode;
            var trackCode = bundle.TrackCode;
            var sectionName = bundle.SectionName;
            var publicationName = bundle.PublicationName;
            var trackTitle = bundle.TrackTitle;

            if (string.IsNullOrWhiteSpace(trackCode))
            {
                logger.Warning(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NoValidTrackFoundForPublication,
                    publicationCode);
                return;
            }

            string languageCodeForLog;
            if (publicationWithoutLanguage)
            {
                languageCodeForLog = "N/A";
            }
            else
            {
                languageCodeForLog = selectedLanguage?.LanguageCode ?? "N/A";
            }

            logger.Information(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.AutoPopulatedSummary,
                languageCodeForLog, publicationCode, sectionCode, trackCode, publicationWithoutLanguage);

            DispatchAutoPopulatedSchedule(
                dispatcher,
                currentSchedule,
                action,
                publicationWithoutLanguage,
                selectedLanguage,
                publicationCode,
                sectionCode,
                trackCode,
                sectionName,
                publicationName,
                trackTitle);

            if (fetchOccurred)
            {
                ReportProgress(1.0, isComplete: true);
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new CategoryFetchProgressMessage(
                    new CategoryFetchProgress { CategoryId = action.CategoryId, Progress = -1, IsComplete = true, HasError = false }));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException)
        {
            logger.Warning(ex, AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NetworkErrorDuringAutoPopulation,
                action.CategoryName);
            PublishAutoPopulateFetchError(action.CategoryId);
            TryRevertToPreviousSchedule(action, dispatcher,
                AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.RevertingToPreviousScheduleStateAfterNetworkError);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.ErrorDuringAutoPopulation,
                action.CategoryName);
            PublishAutoPopulateFetchError(action.CategoryId);
            TryRevertToPreviousSchedule(action, dispatcher,
                AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.RevertingToPreviousScheduleStateAfterError);
        }
    }

    private readonly record struct AutoPopulatePrimaryTrackBundle(
        string PublicationCode,
        string? SectionCode,
        string TrackCode,
        string SectionName,
        string PublicationName,
        string TrackTitle);

    private Language? SelectLanguageForCategory(
        CategorySelectionAction action,
        Dictionary<string, Language> languages)
    {
        Language? selectedLanguage = null;

        if (!string.IsNullOrWhiteSpace(action.PreviousLanguageCode))
        {
            var previousLanguageCode = action.PreviousLanguageCode.ToUpperInvariant();
            if (languages.TryGetValue(previousLanguageCode, out var previousLanguage))
            {
                selectedLanguage = previousLanguage;
                logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.PreservingPreviousLanguageHasPublicationsInCategory,
                    previousLanguageCode, action.CategoryName);
            }
        }

        if (selectedLanguage != null)
        {
            return selectedLanguage;
        }

        if (languages.TryGetValue(AppConstants.Media.DefaultLanguageCode, out var englishLanguage))
        {
            logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedEnglishLanguageDefaultFallback);
            return englishLanguage;
        }

        if (languages.Count > 0)
        {
            using var langEnumerator = languages.Values.GetEnumerator();
            _ = langEnumerator.MoveNext();
            var fallback = langEnumerator.Current;
            logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.EnglishNotFoundSelectedFirstAvailableLanguage,
                fallback.LanguageCode);
            return fallback;
        }

        logger.Warning(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NoLanguagesFoundForCategory,
            action.CategoryName);
        return null;
    }

    private sealed class PublicationCatalogFetchAccumulator
    {
        public bool FetchOccurred { get; set; }
    }

    private async Task<(string? PublicationCode, bool PublicationWithoutLanguage)> ResolvePublicationForCategorySelectionAsync(
        CategorySelectionAction action,
        Language? selectedLanguage,
        MediaDbContext db,
        PublicationCatalogFetchAccumulator publicationsFetchAccumulator)
    {
        if (selectedLanguage != null)
        {
            var languagePick =
                await TryPickPublicationViaLanguagePublicationRowsAsync(action, selectedLanguage, db,
                    publicationsFetchAccumulator);
            if (languagePick != null)
            {
                return languagePick.Value;
            }
        }

        return await TryPickPublicationWithoutLanguageIdAsync(action, db);
    }

    private async Task<(string PublicationCode, bool PublicationWithoutLanguage)?> TryPickPublicationViaLanguagePublicationRowsAsync(
        CategorySelectionAction action,
        Language selectedLanguage,
        MediaDbContext db,
        PublicationCatalogFetchAccumulator publicationsFetchAccumulator)
    {
        var normalizedLanguageCode = selectedLanguage.LanguageCode.ToUpperInvariant();

        var query = db.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode);

        if (!string.IsNullOrWhiteSpace(action.CategoryName))
        {
            query = query.Where(pl => pl.Category != null && pl.Category.CategoryCode == action.CategoryName);
        }

        var publicationLanguages = await query.ToListAsync();
        publicationLanguages = publicationLanguages
            .OrderBy(pl => pl.PublicationCode, PublicationCodeHelper.GetPublicationCodeComparerForCategory(action.CategoryName))
            .ThenBy(pl => pl.Id)
            .ToList();

        foreach (var (pl, publicationCodeForDb) in publicationLanguages.Select(pl =>
                     (pl, PublicationTypeHelper.GetCanonicalPublicationCodeForDatabase(pl.PublicationCode))))
        {
            var chosen = await TryMaterializePublicationForLanguageRowAsync(
                action,
                db,
                pl,
                publicationCodeForDb,
                selectedLanguage,
                normalizedLanguageCode,
                publicationsFetchAccumulator);

            if (chosen != null)
            {
                return chosen.Value;
            }
        }

        return null;
    }

    private async Task<(string PublicationCode, bool PublicationWithoutLanguage)?> TryMaterializePublicationForLanguageRowAsync(
        CategorySelectionAction action,
        MediaDbContext db,
        PublicationLanguage pl,
        string publicationCodeForDb,
        Language selectedLanguage,
        string normalizedLanguageCode,
        PublicationCatalogFetchAccumulator publicationsFetchAccumulator)
    {
        var isAlreadyCataloged = await CategorySelectionAutoPopulateCatalogCheck.CheckIfPublicationWithFirstSectionCatalogedAsync(
            logger, db, pl.PublicationCode, normalizedLanguageCode);

        if (!isAlreadyCataloged)
        {
            publicationsFetchAccumulator.FetchOccurred = true;
            var catalogProgressReporter =
                new Bible.Alarm.Common.Helpers.CategoryFetchProgressReporter(action.CategoryId, default);

            var isCataloged = await languageContentService.EnsurePublicationExistsAsync(
                pl.PublicationCode, selectedLanguage.LanguageCode, catalogProgressReporter);

            if (!isCataloged)
            {
                logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.FailedToCatalogPublicationTryingNext,
                    pl.PublicationCode, selectedLanguage.LanguageCode);
                return null;
            }
        }
        else
        {
            logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.PublicationAlreadyCatalogedWithFirstSectionAndTracks,
                pl.PublicationCode, selectedLanguage.LanguageCode);
        }

        var canQueryWithLanguage = await db.BiblePublications
            .AsNoTracking()
            .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb &&
                            bp.LanguageId != null &&
                            bp.Language != null &&
                            bp.Language.LanguageCode == normalizedLanguageCode);

        if (canQueryWithLanguage)
        {
            logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedPublicationCatalogedCanQueryWithLanguage,
                publicationCodeForDb, selectedLanguage.LanguageCode);
            return (publicationCodeForDb, false);
        }

        logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.PublicationCatalogedCannotQueryWithLanguageTryingNext,
            pl.PublicationCode, selectedLanguage.LanguageCode);

        return null;
    }

    private async Task<(string? PublicationCode, bool PublicationWithoutLanguage)> TryPickPublicationWithoutLanguageIdAsync(
        CategorySelectionAction action,
        MediaDbContext db)
    {
        if (!string.IsNullOrWhiteSpace(action.CategoryName) &&
            (await db.BiblePublications
                    .AsNoTracking()
                    .Where(bp =>
                        bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == action.CategoryName) &&
                        bp.LanguageId == null)
                    .ToListAsync())
                .OrderBy(bp => bp.PublicationCode,
                    PublicationCodeHelper.GetPublicationCodeComparerForCategory(action.CategoryName))
                .ThenBy(bp => bp.Id)
                .FirstOrDefault() is { } pubWithoutLanguage)
        {
            logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedPublicationWithoutLanguageId,
                pubWithoutLanguage.PublicationCode);
            return (pubWithoutLanguage.PublicationCode, true);
        }

        return (null, false);
    }

    private async Task<AutoPopulatePrimaryTrackBundle?> TryResolvePrimaryTrackBundleAsync(
        bool publicationWithoutLanguage,
        Language? selectedLanguage,
        string publicationCode,
        string? categoryName)
    {
        if (publicationWithoutLanguage)
        {
            var (secCode, trkCode, secName, trkTitle, pubName) =
                await BiblePublicationCascadeNoLanguageResolver.GetFirstSectionAndTrackAsync(mediaService, scopeFactory, publicationCode);
            return new AutoPopulatePrimaryTrackBundle(
                publicationCode,
                secCode,
                trkCode ?? string.Empty,
                secName,
                pubName,
                trkTitle);
        }

        if (selectedLanguage == null)
        {
            logger.Warning(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedLanguageNullButPublicationRequiresLanguage);
            return null;
        }

        var languageName = await languageNameService.GetNameAsync(selectedLanguage.Id, AppConstants.Media.DefaultLanguageCode)
            ?? selectedLanguage.LanguageCode;
        var languageModel = new LanguageListViewItemModel(selectedLanguage, languageName);
        var (resultPublicationCode, resultSectionCode, resultTrackCode, resultSectionName, resultPublicationName, resultTrackTitle) =
            await itemSelector.GetPublicationSectionAndTrackForLanguageAsync(languageModel, categoryNameOverride: categoryName);

        if (string.IsNullOrEmpty(resultPublicationCode) || string.IsNullOrWhiteSpace(resultTrackCode))
        {
            logger.Warning(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NoValidTrackFoundForPublicationAndLanguage,
                publicationCode, selectedLanguage.LanguageCode);
            return null;
        }

        return new AutoPopulatePrimaryTrackBundle(
            resultPublicationCode,
            string.IsNullOrWhiteSpace(resultSectionCode) ? null : resultSectionCode,
            resultTrackCode,
            resultSectionName,
            resultPublicationName,
            resultTrackTitle);
    }

    private void DispatchAutoPopulatedSchedule(
        IDispatcher dispatcher,
        ScheduleStateItem currentSchedule,
        CategorySelectionAction action,
        bool publicationWithoutLanguage,
        Language? selectedLanguage,
        string publicationCode,
        string? sectionCode,
        string trackCode,
        string sectionName,
        string publicationName,
        string trackTitle)
    {
        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.BiblePublicationCategoryId = action.CategoryId;
        updatedSchedule.BiblePublicationCategoryName = action.CategoryName;
        updatedSchedule.BiblePublicationIsMusic = string.Equals(action.CategoryName, AppConstants.Media.BiblePublicationCategoryMusic,
            StringComparison.OrdinalIgnoreCase);

        if (publicationWithoutLanguage)
        {
            updatedSchedule.BiblePublicationLanguageCode = AppConstants.Media.DefaultLanguageCode;
            updatedSchedule.BiblePublicationLanguageName = null;
            updatedSchedule.BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            logger.Debug(AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SettingLanguageEnglishDefaultForPublicationWithoutLanguageId,
                publicationCode);
        }
        else if (selectedLanguage != null)
        {
            updatedSchedule.BiblePublicationLanguageCode = selectedLanguage.LanguageCode;
            updatedSchedule.BiblePublicationLanguageName = languageNameService.GetNameCached(selectedLanguage.Id)
                ?? selectedLanguage.LanguageCode;
            updatedSchedule.BiblePublicationLanguageDirection = selectedLanguage.Direction ?? AppConstants.Media.TextDirectionLeftToRight;
        }

        updatedSchedule.BiblePublicationCode = publicationCode;
        updatedSchedule.BiblePublicationName = publicationName;
        updatedSchedule.BiblePublicationSectionCode = SectionCodeHelper.Normalize(sectionCode);
        updatedSchedule.BiblePublicationSectionName = sectionName;
        updatedSchedule.BiblePublicationTrackCode = trackCode;
        updatedSchedule.BiblePublicationTrackTitle = trackTitle;
        updatedSchedule.BiblePublicationFinishedDuration = TimeSpan.Zero;

        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));
    }

    private static void PublishAutoPopulateFetchError(int categoryId) =>
        WeakReferenceMessenger.Default.Send(new CategoryFetchProgressMessage(
            new CategoryFetchProgress { CategoryId = categoryId, Progress = 0, IsComplete = true, HasError = true }));

    private void TryRevertToPreviousSchedule(
        CategorySelectionAction action,
        IDispatcher dispatcher,
        string revertDiagnosticsLogConstant)
    {
        if (action.PreviousScheduleSnapshot == null)
        {
            return;
        }

        logger.Information(revertDiagnosticsLogConstant);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(action.PreviousScheduleSnapshot, true, true, shouldSave: false));
    }

    private static void SendCategoryFetchProgress(int categoryId, double progress, bool isComplete)
    {
        WeakReferenceMessenger.Default.Send(new CategoryFetchProgressMessage(
            new CategoryFetchProgress
            {
                CategoryId = categoryId,
                Progress = progress,
                IsComplete = isComplete
            }));
    }
}
