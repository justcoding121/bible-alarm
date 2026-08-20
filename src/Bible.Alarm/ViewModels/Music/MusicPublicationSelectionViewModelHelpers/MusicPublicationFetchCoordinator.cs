#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

internal sealed class MusicPublicationFetchCoordinator
{
    private readonly IMediaService mediaService;

    public MusicPublicationFetchCoordinator(IMediaService mediaService)
    {
        this.mediaService = mediaService;
    }

    public async Task<Dictionary<string, BiblePublication>?> FetchMusicPublicationsAsync(
        string? languageCode,
        AlarmMusic? current,
        bool downloadAll,
        IFetchProgress? progress,
        CancellationToken cancellationToken = default)
    {
        var isMelodyMusic = string.IsNullOrEmpty(current?.LanguageCode) && string.IsNullOrEmpty(languageCode);
        var effectiveLanguageCode = isMelodyMusic ? AppConstants.Media.DefaultLanguageCode : languageCode;

        if (string.IsNullOrEmpty(effectiveLanguageCode) && !isMelodyMusic)
            return null;

        var languageForFetch = effectiveLanguageCode!;
        Dictionary<string, BiblePublication>? publicationsData;

        if (downloadAll)
        {
            var initialPublications =
                await mediaService.GetBiblePublications(languageForFetch, AppConstants.Media.BiblePublicationCategoryMusic,
                    downloadAll: false, null, requireIsMusicForMusicCategory: true);
            var expectedPublicationCount =
                await mediaService.GetExpectedPublicationCountAsync(languageForFetch,
                    AppConstants.Media.BiblePublicationCategoryMusic, requireIsMusicForMusicCategory: true);
            if (ArePublicationsFullyCataloged(initialPublications, expectedPublicationCount))
            {
                Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AllExpectedAlreadyCatalogedSkippingFetch,
                    expectedPublicationCount, languageForFetch, AppConstants.Media.BiblePublicationCategoryMusic);
                return initialPublications;
            }
        }

        if (isMelodyMusic)
        {
            publicationsData = await mediaService.GetBiblePublications(AppConstants.Media.DefaultLanguageCode,
                AppConstants.Media.BiblePublicationCategoryMusic, downloadAll, progress,
                requireIsMusicForMusicCategory: true);
        }
        else if (!string.IsNullOrEmpty(languageCode))
        {
            publicationsData =
                await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic,
                    downloadAll, progress, requireIsMusicForMusicCategory: true);
        }
        else
        {
            return null;
        }

        if (downloadAll && !string.IsNullOrEmpty(languageCode) &&
            !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            publicationsData = await RetryFetchUntilCatalogedAsync(languageCode, progress, cancellationToken);
        }

        if (publicationsData == null && !string.IsNullOrEmpty(languageCode))
        {
            publicationsData =
                await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic,
                    downloadAll, progress, requireIsMusicForMusicCategory: true);
        }

        return publicationsData;
    }

    private static bool ArePublicationsFullyCataloged(Dictionary<string, BiblePublication>? publications, int expectedPublicationCount)
    {
        var actualCount = publications?.Values.Count ?? 0;
        if (actualCount < expectedPublicationCount || publications == null || publications.Count == 0)
        {
            return false;
        }

        return CountCatalogedPublications(publications) == actualCount;
    }

    private static int CountCatalogedPublications(Dictionary<string, BiblePublication>? publications)
    {
        return publications?.Values.Count(static p =>
            !string.IsNullOrEmpty(p.Name) &&
            !string.Equals(p.Name, p.PublicationCode, StringComparison.OrdinalIgnoreCase) &&
            p.Id > 0) ?? 0;
    }

    private async Task<Dictionary<string, BiblePublication>?> RetryFetchUntilCatalogedAsync(
        string languageCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        var initialPublications =
            await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic,
                downloadAll: false, null, requireIsMusicForMusicCategory: true);

        var expectedPublicationCount =
            await mediaService.GetExpectedPublicationCountAsync(languageCode, AppConstants.Media.BiblePublicationCategoryMusic,
                requireIsMusicForMusicCategory: true);
        if (ArePublicationsFullyCataloged(initialPublications, expectedPublicationCount))
        {
            Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AllExpectedAlreadyCatalogedSkippingFetch,
                expectedPublicationCount, languageCode, AppConstants.Media.BiblePublicationCategoryMusic);
            return initialPublications;
        }

        Serilog.Log.Information(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.StartingFetchWithRetries,
            languageCode, AppConstants.Media.BiblePublicationCategoryMusic);

        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        CatalogRetryLoopResult<Dictionary<string, BiblePublication>> loopOutcome;
        try
        {
            loopOutcome = await CatalogRetryLoop.RunAsync(
                ctx => RunMusicPublicationRetryIterationAsync(languageCode, progress, ctx),
                (ex, attempt) => Serilog.Log.Warning(ex, AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AttemptFailedWillRetry,
                    attempt, languageCode),
                cancellationToken);
        }
        finally
        {
            progress?.SetIsVisible(false);
        }

        if (!loopOutcome.AllCataloged)
        {
            Serilog.Log.Warning(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.TimeoutAfterAttemptsWaitingForCatalog,
                loopOutcome.Attempt, languageCode);

            return await TryRecoverMusicPublicationsAfterCatalogRetryTimeoutAsync(languageCode, progress, loopOutcome.Data);
        }

        return loopOutcome.Data;
    }

    private async Task<Dictionary<string, BiblePublication>?> TryRecoverMusicPublicationsAfterCatalogRetryTimeoutAsync(
        string languageCode,
        IFetchProgress? progress,
        Dictionary<string, BiblePublication>? publicationsData)
    {
        if (publicationsData != null && publicationsData.Count > 0)
        {
            return publicationsData;
        }

        try
        {
            return await mediaService.GetBiblePublications(languageCode,
                AppConstants.Media.BiblePublicationCategoryMusic,
                downloadAll: false, progress, requireIsMusicForMusicCategory: true);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.FinalFetchAttemptFailed,
                languageCode);
            return publicationsData;
        }
    }

    private async Task<CatalogRetryIterationResult<Dictionary<string, BiblePublication>>> RunMusicPublicationRetryIterationAsync(
        string languageCode,
        IFetchProgress? progress,
        CatalogRetryIterationContext context)
    {
        var fetchedWithProgress =
            await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic,
                downloadAll: true, progress, requireIsMusicForMusicCategory: true);

        await Task.Delay(500, context.CancellationToken);

        var reQueriedData =
            await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic,
                downloadAll: false, null, requireIsMusicForMusicCategory: true);

        var retryExpectedCount =
            await mediaService.GetExpectedPublicationCountAsync(languageCode, AppConstants.Media.BiblePublicationCategoryMusic,
                requireIsMusicForMusicCategory: true);
        var retryActualCount = reQueriedData?.Values.Count ?? 0;
        var retryHasAllExpected = retryActualCount >= retryExpectedCount;
        var retryAllCataloged = ArePublicationsFullyCataloged(reQueriedData, retryExpectedCount);

        if (retryAllCataloged)
        {
            Serilog.Log.Information(
                AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AllExpectedPublicationsCatalogedOnAttempt,
                retryExpectedCount, context.Attempt, languageCode);
            return CatalogRetryIterationResult<Dictionary<string, BiblePublication>>.Success(reQueriedData!);
        }

        var currentCatalogedCount = CountCatalogedPublications(reQueriedData);

        if (currentCatalogedCount > 0 && currentCatalogedCount <= context.PreviousCatalogedCount)
        {
            Serilog.Log.Information(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.NoProgressBetweenRetriesStopping,
                currentCatalogedCount, retryExpectedCount, languageCode);
            return CatalogRetryIterationResult<Dictionary<string, BiblePublication>>.Stagnation(reQueriedData);
        }

        LogMusicPublicationRetryDiagnostics(context.Attempt, reQueriedData, retryHasAllExpected, retryActualCount,
            retryExpectedCount);

        await Task.Delay(CatalogRetryLoop.DelayMilliseconds(context.Attempt), context.CancellationToken);

        return CatalogRetryIterationResult<Dictionary<string, BiblePublication>>.Continue(fetchedWithProgress, currentCatalogedCount);
    }

    private static void LogMusicPublicationRetryDiagnostics(
        int attempt,
        Dictionary<string, BiblePublication>? reQueriedData,
        bool retryHasAllExpected,
        int retryActualCount,
        int retryExpectedCount)
    {
        if (reQueriedData != null)
        {
            var placeholders = reQueriedData.Values
                .Where(static p =>
                    string.IsNullOrEmpty(p.Name) ||
                    string.Equals(p.Name, p.PublicationCode, StringComparison.OrdinalIgnoreCase) ||
                    p.Id == 0)
                .Select(p => p.PublicationCode)
                .ToList();

            if (placeholders.Count > 0)
            {
                Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AttemptStillWaitingForPlaceholders,
                    attempt, placeholders.Count, string.Join(", ", placeholders));
                return;
            }

            if (!retryHasAllExpected)
            {
                Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AttemptPartialPublicationsRetry,
                    attempt, retryActualCount, retryExpectedCount);
            }

            return;
        }

        Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AttemptNoPublicationsYetRetry,
            attempt);
    }
}
