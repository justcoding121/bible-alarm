#nullable enable
using System.Net.Sockets;
using AutoMapper;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
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
    private readonly MusicPublicationSelectionDataProvider dataProvider;
    private readonly MusicPublicationSelectionPropertyManager propertyManager;
    private readonly IMapper mapper;

    public MusicPublicationSelectionRefreshHandler(
        IState<ApplicationState> state,
        MusicPublicationSelectionStateManager stateManager,
        MusicPublicationSelectionDataProvider dataProvider,
        MusicPublicationSelectionPropertyManager propertyManager,
        IMapper mapper)
    {
        this.state = state;
        this.stateManager = stateManager;
        this.dataProvider = dataProvider;
        this.propertyManager = propertyManager;
        this.mapper = mapper;
    }

    public async Task RefreshAsync(
        CancellationTokenSource fetchCts,
        Func<string?, Task> populateLanguages,
        Func<string?, bool, IFetchProgress?, CancellationToken, Task> populateSongPublications,
        Action setSelectedSongPublication)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            propertyManager.CanCancelFetch = true;
            DeviceDisplay.Current.KeepScreenOn = true;
        });

        string? newLanguageCode = null;
        bool isMelodyMusic = false;

        try
        {
            for (int i = 0; i < MaxWaitAttempts; i++)
            {
                fetchCts.Token.ThrowIfCancellationRequested();
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

                await Task.Delay(DelayMs, fetchCts.Token);
            }

            var finalStateValue = state.Value;
            if (finalStateValue.CurrentSchedule == null)
                return;

            var current = MusicPublicationSelectionStateManager.GetCurrentFromState(state);
            if (current != null)
                stateManager.EnsureCurrentIsSet(state, mapper);

            if (!isMelodyMusic)
            {
                if (propertyManager.Languages == null || propertyManager.Languages.Count == 0)
                    await populateLanguages(null);

                propertyManager.SetupLanguageSearchHandler(async (searchTerm) => await populateLanguages(searchTerm));
            }

            string? languageCodeToUse = ResolveLanguageCodeToUse(
                isMelodyMusic,
                newLanguageCode,
                finalStateValue.CurrentSchedule.MusicLanguageCode,
                current);

            var progressReporter = new ModalOverlayFetchProgressReporter("MusicPublication", fetchCts.Token);

            if (isMelodyMusic)
                await populateSongPublications(null, true, progressReporter, fetchCts.Token);
            else if (!string.IsNullOrEmpty(languageCodeToUse))
                await populateSongPublications(languageCodeToUse, true, progressReporter, fetchCts.Token);
            else
                setSelectedSongPublication();

            setSelectedSongPublication();
        }
        catch (OperationCanceledException)
        {
            Serilog.Log.Debug("MusicPublicationSelectionViewModel: Fetch cancelled by user");
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.ShowProgress = false);
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException)
        {
            Serilog.Log.Warning(ex, "MusicPublicationSelectionViewModel: Fetch failed with network error");
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.ShowProgress = false);
            throw;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "MusicPublicationSelectionViewModel: Fetch failed during refresh");
            await MainThread.InvokeOnMainThreadAsync(() => propertyManager.ShowProgress = false);
            throw;
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                propertyManager.CanCancelFetch = false;
                propertyManager.ShowProgress = false;
                DeviceDisplay.Current.KeepScreenOn = false;
            });
        }
    }

    private string? ResolveLanguageCodeToUse(
        bool isMelodyMusic,
        string? newLanguageCode,
        string? scheduleLanguageCode,
        AlarmMusic? current)
    {
        if (!isMelodyMusic &&
            propertyManager.CurrentLanguage == null &&
            propertyManager.Languages != null &&
            propertyManager.Languages.Count > 0)
        {
            var langCode = scheduleLanguageCode ?? newLanguageCode;
            var languageToSelect = !string.IsNullOrEmpty(langCode)
                ? propertyManager.Languages.FirstOrDefault(l => l.Code == langCode)
                : null;

            if (languageToSelect == null)
                languageToSelect = propertyManager.Languages.FirstOrDefault(l => l.Code == AppConstants.Media.DefaultLanguageCode)
                    ?? (propertyManager.Languages.Count > 0 ? propertyManager.Languages[0] : null);

            if (languageToSelect != null)
            {
                propertyManager.CurrentLanguage = languageToSelect;
                languageToSelect.IsSelected = true;
                return languageToSelect.Code;
            }
        }

        if (propertyManager.CurrentLanguage != null)
            return propertyManager.CurrentLanguage.Code;

        if (current != null && !string.IsNullOrEmpty(current.LanguageCode))
            return current.LanguageCode;

        return newLanguageCode;
    }
}
