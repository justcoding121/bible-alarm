#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
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

        var retryDelay = 1000;
        var allCataloged = false;
        var attempt = 0;
        Dictionary<string, BiblePublication>? publicationsData = null;

        try
        {
            var loopOutcome = await RunMusicPublicationCatalogRetryLoopAsync(languageCode, progress, cancellationToken, retryDelay);
            publicationsData = loopOutcome.PublicationsData;
            allCataloged = loopOutcome.AllCataloged;
            attempt = loopOutcome.Attempt;
        }
        finally
        {
            progress?.SetIsVisible(false);
        }

        if (!allCataloged)
        {
            Serilog.Log.Warning(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.TimeoutAfterAttemptsWaitingForCatalog,
                attempt, languageCode);

            publicationsData = await TryRecoverMusicPublicationsAfterCatalogRetryTimeoutAsync(languageCode, progress, publicationsData);
        }

        return publicationsData;
    }

    private sealed class MusicPublicationCatalogRetryLoopOutcome
    {
        public bool AllCataloged { get; init; }
        public int Attempt { get; init; }
        public Dictionary<string, BiblePublication>? PublicationsData { get; init; }
    }

    private async Task<MusicPublicationCatalogRetryLoopOutcome> RunMusicPublicationCatalogRetryLoopAsync(
        string languageCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken,
        int retryDelay)
    {
        const int maxRetries = 10;
        var maxWaitTime = TimeSpan.FromSeconds(60);
        var startTime = DateTime.UtcNow;
        var allCataloged = false;
        var attempt = 0;
        var previousCatalogedCount = -1;
        Dictionary<string, BiblePublication>? publicationsData = null;

        while (!allCataloged && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            attempt++;

            try
            {
                var outcome =
                    await RunMusicPublicationRetryIterationAsync(languageCode, progress, cancellationToken, attempt,
                        retryDelay, previousCatalogedCount);

                if (ApplyMusicPublicationCatalogIterationOutcome(
                        outcome,
                        ref publicationsData,
                        ref allCataloged,
                        ref previousCatalogedCount,
                        languageCode,
                        attempt))
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                await HandleMusicPublicationCatalogRetryExceptionAsync(
                    ex, attempt, languageCode, retryDelay, cancellationToken);
            }
        }

        return new MusicPublicationCatalogRetryLoopOutcome
        {
            AllCataloged = allCataloged,
            Attempt = attempt,
            PublicationsData = publicationsData
        };
    }

    private static bool ApplyMusicPublicationCatalogIterationOutcome(
        MusicPublicationRetryIterationOutcome outcome,
        ref Dictionary<string, BiblePublication>? publicationsData,
        ref bool allCataloged,
        ref int previousCatalogedCount,
        string languageCode,
        int attempt)
    {
        publicationsData = outcome.NextSnapshot;
        if (outcome.Completed)
        {
            allCataloged = true;
            if (outcome.LogSuccess)
            {
                Serilog.Log.Information(
                    AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AllExpectedPublicationsCatalogedOnAttempt,
                    outcome.ExpectedCount, attempt, languageCode);
            }

            return false;
        }

        if (outcome.BreakRetries)
        {
            return true;
        }

        previousCatalogedCount = outcome.UpdatedCatalogedCount;
        return false;
    }

    private static async Task HandleMusicPublicationCatalogRetryExceptionAsync(
        Exception ex,
        int attempt,
        string languageCode,
        int retryDelay,
        CancellationToken cancellationToken)
    {
        if (NetworkExceptionHelper.ShouldRethrowFromCatalogRetryLoop(ex))
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        Serilog.Log.Warning(ex, AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AttemptFailedWillRetry,
            attempt, languageCode);

        var delay = Math.Min(retryDelay * attempt, 5000);
        await Task.Delay(delay, cancellationToken);
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

    private async Task<MusicPublicationRetryIterationOutcome> RunMusicPublicationRetryIterationAsync(
        string languageCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken,
        int attempt,
        int retryDelayBase,
        int previousCatalogedCount)
    {
        var fetchedWithProgress =
            await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic,
                downloadAll: true, progress, requireIsMusicForMusicCategory: true);

        await Task.Delay(500, cancellationToken);

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
            return MusicPublicationRetryIterationOutcome.ForSuccess(reQueriedData!, retryExpectedCount);
        }

        var currentCatalogedCount = CountCatalogedPublications(reQueriedData);

        if (currentCatalogedCount > 0 && currentCatalogedCount <= previousCatalogedCount)
        {
            Serilog.Log.Information(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.NoProgressBetweenRetriesStopping,
                currentCatalogedCount, retryExpectedCount, languageCode);
            return MusicPublicationRetryIterationOutcome.ForStagnation(reQueriedData);
        }

        LogMusicPublicationRetryDiagnostics(attempt, reQueriedData, retryHasAllExpected, retryActualCount,
            retryExpectedCount);

        var delay = Math.Min(retryDelayBase * attempt, 5000);
        await Task.Delay(delay, cancellationToken);

        return MusicPublicationRetryIterationOutcome.ForContinue(fetchedWithProgress, currentCatalogedCount);
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

    private sealed record MusicPublicationRetryIterationOutcome(
        bool Completed,
        bool LogSuccess,
        bool BreakRetries,
        Dictionary<string, BiblePublication>? NextSnapshot,
        int ExpectedCount,
        int UpdatedCatalogedCount)
    {
        public static MusicPublicationRetryIterationOutcome ForSuccess(
            Dictionary<string, BiblePublication> reQueried,
            int expectedCount) =>
            new(true, LogSuccess: true, BreakRetries: false, NextSnapshot: reQueried, ExpectedCount: expectedCount,
                UpdatedCatalogedCount: -1);

        public static MusicPublicationRetryIterationOutcome ForStagnation(Dictionary<string, BiblePublication>? reQueried) =>
            new(false, LogSuccess: false, BreakRetries: true, NextSnapshot: reQueried, ExpectedCount: 0,
                UpdatedCatalogedCount: -1);

        public static MusicPublicationRetryIterationOutcome ForContinue(
            Dictionary<string, BiblePublication>? reQueried,
            int updatedCatalogedCount) =>
            new(false, LogSuccess: false, BreakRetries: false, NextSnapshot: reQueried, ExpectedCount: 0,
                UpdatedCatalogedCount: updatedCatalogedCount);
    }
}
