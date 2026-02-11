#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;

/// <summary>
/// Handles section tap in Bible publication section selection: build selection, dispatch, close modal; on error show toast and close.
/// </summary>
internal sealed class BiblePublicationSectionSelectionTrackTapHandler
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly TrackSelectionResolver trackSelectionResolver;

    public BiblePublicationSectionSelectionTrackTapHandler(
        ILogger logger,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        TrackSelectionResolver trackSelectionResolver)
    {
        this.logger = logger;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.trackSelectionResolver = trackSelectionResolver;
    }

    public async Task HandleSectionTapAsync(BiblePublicationSectionListViewItemModel sectionItem)
    {
        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null || string.IsNullOrEmpty(currentSchedule.BiblePublicationCode))
            {
                logger.Warning("BiblePublicationSectionSelectionViewModel: TrackSelectionCommand - CurrentSchedule is null or PublicationCode is empty");
                return;
            }

            if (string.IsNullOrWhiteSpace(sectionItem.Section.SectionCode))
            {
                logger.Warning("BiblePublicationSectionSelectionViewModel: TrackSelectionCommand - Invalid section code for publication={PublicationCode}",
                    currentSchedule.BiblePublicationCode);
                return;
            }

            var biblePublicationItem = await trackSelectionResolver.BuildSelectionAsync(
                sectionItem,
                currentSchedule,
                () => ServiceProviderManager.GetService<ILanguageContentService>());

            if (biblePublicationItem == null)
            {
                return;
            }

            dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
            await navigationService.PopModalAsync();
        }
        catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
        {
            logger.Warning(ex, "BiblePublicationSectionSelectionViewModel: TrackSelectionCommand - Network error selecting section");
            await MainThread.InvokeOnMainThreadAsync(() => sectionItem.DownloadProgress = 0.0);
            var toastService = ServiceProviderManager.GetService<IToastService>();
            await Task.Delay(500);
            await navigationService.PopModalAsync();
            await toastService!.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "BiblePublicationSectionSelectionViewModel: TrackSelectionCommand - Error selecting section");
            await MainThread.InvokeOnMainThreadAsync(() => sectionItem.DownloadProgress = 0.0);
            var toastService = ServiceProviderManager.GetService<IToastService>();
            await Task.Delay(500);
            await navigationService.PopModalAsync();
            await toastService!.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
        }
    }
}
