#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;

public class StateChangeHandler
{
    public sealed record Callbacks(
        Action<BiblePublicationSchedule> SetCurrent,
        Action<BiblePublicationSchedule> SetLastCurrent,
        Func<bool> GetInitComplete,
        Action<bool> SetIsBusy,
        Action<string, string> Initialize,
        Action SetSelectedSection);

    private readonly ILogger logger;
    private readonly Action<BiblePublicationSchedule> setCurrent;
    private readonly Action<BiblePublicationSchedule> setLastCurrent;
    private readonly Func<bool> getInitComplete;
    private readonly Action<bool> setIsBusy;
    private readonly Action<string, string> initialize;
    private readonly Action setSelectedSection;

    // Track last language and publication code to detect changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;

    public StateChangeHandler(ILogger logger, Callbacks callbacks)
    {
        this.logger = logger;
        setCurrent = callbacks.SetCurrent;
        setLastCurrent = callbacks.SetLastCurrent;
        getInitComplete = callbacks.GetInitComplete;
        setIsBusy = callbacks.SetIsBusy;
        initialize = callbacks.Initialize;
        setSelectedSection = callbacks.SetSelectedSection;
    }

    public void HandleStateChanged(ApplicationState stateValue)
    {
        // Use CurrentSchedule as the source of truth, not CurrentBiblePublicationSchedule
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;
        var newPublicationCode = currentSchedule.BiblePublicationCode;

        if (string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = languageChanged || publicationCodeChanged;

        if (!needsRepopulation && getInitComplete())
        {
            return;
        }

        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        var sectionCode = currentSchedule.BiblePublicationSectionCode;
        var newCurrent = new BiblePublicationSchedule
        {
            LanguageCode = newLanguageCode,
            PublicationCode = newPublicationCode ?? string.Empty,
            SectionCode = sectionCode,
            TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty,
            FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
        };
        setCurrent(newCurrent);
        setLastCurrent(newCurrent);

        if (needsRepopulation && getInitComplete())
        {
            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
                    initialize(newLanguageCode, newPublicationCode ?? string.Empty);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "BiblePublicationSectionSelectionViewModel: OnBiblePublicationChanged - Error during repopulation");
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
