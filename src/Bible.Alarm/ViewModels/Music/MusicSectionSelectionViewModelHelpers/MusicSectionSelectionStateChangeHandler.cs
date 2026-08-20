#nullable enable

using Bible.Alarm.Stores;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

/// <summary>
/// Handles state change logic for MusicSectionSelectionViewModel.
/// Similar to BiblePublicationSectionSelectionViewModelHelpers (Bible publications) but for music.
/// Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal.
/// </summary>
public class MusicSectionSelectionStateChangeHandler
{
    public sealed record Callbacks(
        Action<string?> SetLastPublicationCode,
        Action<string?> SetLastSectionCode,
        Func<bool> GetInitComplete,
        Action<bool> SetIsBusy,
        Action<string> Initialize,
        Action SetSelectedSection);

    private readonly ILogger logger;
    private readonly Action<string?> setLastPublicationCode;
    private readonly Action<string?> setLastSectionCode;
    private readonly Func<bool> getInitComplete;
    private readonly Action<bool> setIsBusy;
    private readonly Action<string> initialize;
    private readonly Action setSelectedSection;

    // Track last values to detect changes
    private string? lastPublicationCode;
    private string? lastSectionCode;

    public MusicSectionSelectionStateChangeHandler(ILogger logger, Callbacks callbacks)
    {
        this.logger = logger;
        setLastPublicationCode = callbacks.SetLastPublicationCode;
        setLastSectionCode = callbacks.SetLastSectionCode;
        getInitComplete = callbacks.GetInitComplete;
        setIsBusy = callbacks.SetIsBusy;
        initialize = callbacks.Initialize;
        setSelectedSection = callbacks.SetSelectedSection;
    }

    public void HandleStateChanged(ApplicationState stateValue)
    {
        // Use CurrentSchedule as the source of truth
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;

        if (string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
        {
            return;
        }

        var newPublicationCode = currentSchedule.MusicPublicationCode;
        var newSectionCode = currentSchedule.MusicSectionCode;

        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = publicationCodeChanged;

        if (!needsRepopulation && getInitComplete())
        {
            var sectionCodeChanged = lastSectionCode != newSectionCode;
            if (sectionCodeChanged)
            {
                lastSectionCode = newSectionCode;
                setLastSectionCode(newSectionCode);
                // Update selected section when state changes (e.g., after navigating back)
                MainThread.BeginInvokeOnMainThread(setSelectedSection);
            }
            return;
        }

        lastPublicationCode = newPublicationCode;
        lastSectionCode = newSectionCode;
        setLastPublicationCode(newPublicationCode);
        setLastSectionCode(newSectionCode);

        if (needsRepopulation && getInitComplete())
        {
            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
                    initialize(newPublicationCode);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "[MusicSectionSelection] StateChangeHandler: HandleStateChanged - Error during repopulation");
                }
                finally
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(false));
                }
            });
        }
        else
        {
            // Update selected section when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(setSelectedSection);
        }
    }
}
