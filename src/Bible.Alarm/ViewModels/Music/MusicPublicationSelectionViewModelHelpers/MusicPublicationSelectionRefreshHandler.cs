#nullable enable
using System.Net.Sockets;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

/// <summary>
/// Handles RefreshFromState for MusicPublicationSelectionViewModel modal.
/// </summary>
public sealed class MusicPublicationSelectionRefreshHandler
{
    private const int MaxWaitAttempts = 10;
    private const int DelayMs = 100;

    private readonly IState<ApplicationState> state;
    private readonly MusicPublicationSelectionStateManager stateManager;
    private readonly Func<System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>> getLanguages;
    private readonly Func<LanguageListViewItemModel?> getCurrentLanguage;
    private readonly Action<LanguageListViewItemModel?> setCurrentLanguage;
    private readonly Action<Func<string?, Task>> setupLanguageSearchHandler;
    private readonly Action<bool> setShowProgress;
    private readonly Action<bool> setCanCancelFetch;

    public MusicPublicationSelectionRefreshHandler(
        IState<ApplicationState> state,
        MusicPublicationSelectionStateManager stateManager,
        Func<System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>> getLanguages,
        Func<LanguageListViewItemModel?> getCurrentLanguage,
        Action<LanguageListViewItemModel?> setCurrentLanguage,
        Action<Func<string?, Task>> setupLanguageSearchHandler,
        Action<bool> setShowProgress,
        Action<bool> setCanCancelFetch)
    {
        this.state = state;
        this.stateManager = stateManager;
        this.getLanguages = getLanguages;
        this.getCurrentLanguage = getCurrentLanguage;
        this.setCurrentLanguage = setCurrentLanguage;
        this.setupLanguageSearchHandler = setupLanguageSearchHandler;
        this.setShowProgress = setShowProgress;
        this.setCanCancelFetch = setCanCancelFetch;
    }

    public async Task RefreshAsync(
        CancellationTokenSource fetchCts,
        Func<string?, Task> populateLanguages,
        Func<string?, bool, IFetchProgress?, CancellationToken, Task> populateSongPublications,
        Action setSelectedSongPublication)
    {
        await BeginFetchUiAsync();

        try
        {
            var snapshot = await WaitForMusicPublicationCodesAsync(fetchCts.Token);

            var finalStateValue = state.Value;
            if (finalStateValue.CurrentSchedule == null)
                return;

            var current = MusicPublicationSelectionStateManager.GetCurrentFromState(state);
            if (current != null)
                stateManager.EnsureCurrentIsSet(state);

            if (!snapshot.IsMelodyMusic)
            {
                var languages = getLanguages();
                if (languages == null || languages.Count == 0)
                    await populateLanguages(null);

                setupLanguageSearchHandler(async (searchTerm) => await populateLanguages(searchTerm));
            }

            string? languageCodeToUse = ResolveLanguageCodeToUse(
                snapshot.IsMelodyMusic,
                snapshot.NewLanguageCode,
                finalStateValue.CurrentSchedule.MusicLanguageCode,
                current);

            var progressReporter = new ModalOverlayFetchProgressReporter("MusicPublication", fetchCts.Token);

            if (snapshot.IsMelodyMusic)
                await populateSongPublications(null, true, progressReporter, fetchCts.Token);
            else if (!string.IsNullOrEmpty(languageCodeToUse))
                await populateSongPublications(languageCodeToUse, true, progressReporter, fetchCts.Token);
            else
                setSelectedSongPublication();

            setSelectedSongPublication();
        }
        catch (OperationCanceledException ex)
        {
            Serilog.Log.Debug(ex, AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.FetchCancelledByUser);
            await MainThread.InvokeOnMainThreadAsync(() => setShowProgress(false));
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() => setShowProgress(false));
            throw new InvalidOperationException(
                AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.FetchFailedNetworkError,
                ex);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => setShowProgress(false));
            throw new InvalidOperationException(
                AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.FetchFailedDuringRefresh,
                ex);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                setCanCancelFetch(false);
                setShowProgress(false);
                DeviceDisplay.Current.KeepScreenOn = false;
            });
        }
    }

    private async Task BeginFetchUiAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            setCanCancelFetch(true);
            DeviceDisplay.Current.KeepScreenOn = true;
        });
    }

    private async Task<(string? NewLanguageCode, bool IsMelodyMusic)> WaitForMusicPublicationCodesAsync(CancellationToken ct)
    {
        string? newLanguageCode = null;
        bool isMelodyMusic = false;

        for (var i = 0; i < MaxWaitAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            var stateValue = state.Value;

            if (stateValue.CurrentSchedule != null && !string.IsNullOrEmpty(stateValue.CurrentSchedule.MusicPublicationCode))
            {
                newLanguageCode = stateValue.CurrentSchedule.MusicLanguageCode;
                isMelodyMusic = string.IsNullOrEmpty(newLanguageCode);

                if (!isMelodyMusic && !string.IsNullOrEmpty(newLanguageCode))
                    break;
                if (isMelodyMusic)
                    break;
            }

            await Task.Delay(DelayMs, ct);
        }

        return (newLanguageCode, isMelodyMusic);
    }

    private string? ResolveLanguageCodeToUse(
        bool isMelodyMusic,
        string? newLanguageCode,
        string? scheduleLanguageCode,
        AlarmMusic? current)
    {
        var languages = getLanguages();
        var currentLanguage = getCurrentLanguage();

        if (!isMelodyMusic &&
            currentLanguage == null &&
            languages != null &&
            languages.Count > 0)
        {
            var langCode = scheduleLanguageCode ?? newLanguageCode;
            var languageToSelect = !string.IsNullOrEmpty(langCode)
                ? languages.FirstOrDefault(l => string.Equals(l.Code, langCode, StringComparison.OrdinalIgnoreCase))
                : null;

            if (languageToSelect == null)
            {
                languageToSelect = languages.FirstOrDefault(l => string.Equals(l.Code, AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase));
                if (languageToSelect == null && languages.Count > 0)
                {
                    languageToSelect = languages[0];
                }
            }

            if (languageToSelect != null)
            {
                setCurrentLanguage(languageToSelect);
                languageToSelect.IsSelected = true;
                return languageToSelect.Code;
            }
        }

        if (currentLanguage != null)
            return currentLanguage.Code;

        if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            return current.LanguageCode;

        return newLanguageCode;
    }
}
