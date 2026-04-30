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

    public MusicSectionSelectionStateChangeHandler(
        ILogger logger,
        Action<string?> setLastPublicationCode,
        Action<string?> setLastSectionCode,
        Func<bool> getInitComplete,
        Action<bool> setIsBusy,
        Action<string> initialize,
        Action setSelectedSection)
    {
        this.logger = logger;
        this.setLastPublicationCode = setLastPublicationCode;
        this.setLastSectionCode = setLastSectionCode;
        this.getInitComplete = getInitComplete;
        this.setIsBusy = setIsBusy;
        this.initialize = initialize;
        this.setSelectedSection = setSelectedSection;
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

        // Check if publication code changed (need to repopulate sections)
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = publicationCodeChanged;

        // If no changes detected and we're already initialized, skip
        if (!needsRepopulation && getInitComplete())
        {
            // Only update selected section if section code changed
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

        // Update tracking variables
        lastPublicationCode = newPublicationCode;
        lastSectionCode = newSectionCode;
        setLastPublicationCode(newPublicationCode);
        setLastSectionCode(newSectionCode);

        // If publication code changed, repopulate sections
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
