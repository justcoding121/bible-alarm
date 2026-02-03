#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.BiblePublications;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

/// <summary>
/// Handles state change logic for MusicSectionSelectionViewModel.
/// Similar to BiblePublicationSectionSelectionViewModelHelpers (Bible publications) but for music.
/// Music container always uses "Music" category and filters by MusicType (Music vs VocalMusic).
/// </summary>
public class MusicSectionSelectionStateChangeHandler
{
    private readonly ILogger logger;
    private readonly Func<string?> getLastPublicationCode;
    private readonly Action<string?> setLastPublicationCode;
    private readonly Func<MusicType?> getLastMusicType;
    private readonly Action<MusicType?> setLastMusicType;
    private readonly Func<string?> getLastSectionCode;
    private readonly Action<string?> setLastSectionCode;
    private readonly Func<bool> getInitComplete;
    private readonly Action<bool> setIsBusy;
    private readonly Func<ObservableCollection<BiblePublicationSectionListViewItemModel>?> getSections;
    private readonly Action<string> initialize;
    private readonly Action setSelectedSection;

    // Track last values to detect changes
    private string? lastPublicationCode;
    private MusicType? lastMusicType;
    private string? lastSectionCode;

    public MusicSectionSelectionStateChangeHandler(
        ILogger logger,
        Func<string?> getLastPublicationCode,
        Action<string?> setLastPublicationCode,
        Func<MusicType?> getLastMusicType,
        Action<MusicType?> setLastMusicType,
        Func<string?> getLastSectionCode,
        Action<string?> setLastSectionCode,
        Func<bool> getInitComplete,
        Action<bool> setIsBusy,
        Func<ObservableCollection<BiblePublicationSectionListViewItemModel>?> getSections,
        Action<string> initialize,
        Action setSelectedSection)
    {
        this.logger = logger;
        this.getLastPublicationCode = getLastPublicationCode;
        this.setLastPublicationCode = setLastPublicationCode;
        this.getLastMusicType = getLastMusicType;
        this.setLastMusicType = setLastMusicType;
        this.getLastSectionCode = getLastSectionCode;
        this.setLastSectionCode = setLastSectionCode;
        this.getInitComplete = getInitComplete;
        this.setIsBusy = setIsBusy;
        this.getSections = getSections;
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
        
        // Only handle instrumental music (Music) for now
        // Vocal music sections use the Bible publication section selection
        if (!currentSchedule.MusicType.HasValue || 
            currentSchedule.MusicType.Value != MusicType.Music ||
            string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
        {
            return;
        }

        var newPublicationCode = currentSchedule.MusicPublicationCode;
        var newMusicType = currentSchedule.MusicType.Value;
        var newSectionCode = currentSchedule.MusicSectionCode;

        // Check if publication code or music type changed (need to repopulate sections)
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var musicTypeChanged = lastMusicType != newMusicType;
        var needsRepopulation = publicationCodeChanged || musicTypeChanged;

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
        lastMusicType = newMusicType;
        lastSectionCode = newSectionCode;
        setLastPublicationCode(newPublicationCode);
        setLastMusicType(newMusicType);
        setLastSectionCode(newSectionCode);

        // If publication code or music type changed, repopulate sections
        if (needsRepopulation && getInitComplete())
        {
            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setIsBusy(true));
                    initialize(newPublicationCode);
                    // Note: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "[MusicSectionSelection] StateChangeHandler: HandleStateChanged - Error during repopulation");
                    // Note: Do NOT set IsBusy = false here - the modal controls this
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
