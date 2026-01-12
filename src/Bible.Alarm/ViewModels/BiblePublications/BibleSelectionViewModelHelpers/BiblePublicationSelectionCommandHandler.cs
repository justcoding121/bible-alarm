#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for bible selection operations.
/// </summary>
public sealed class BiblePublicationSelectionCommandHandler
{
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    public BiblePublicationSelectionCommandHandler(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IMapper mapper,
        IBiblePublicationService? biblePublicationService = null)
    {
        this.mediaService = mediaService;
        this.biblePublicationService = biblePublicationService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;
    }

    public ICommand CreateSectionSelectionCommand(
        Func<LanguageListViewItemModel?> getCurrentLanguage,
        Func<ObservableCollection<PublicationListViewItemModel>> getPublications,
        Func<Dictionary<string, PublicationListViewItemModel>> getPublicationVMsMapping,
        Func<BiblePublicationSchedule?> getCurrent)
    {
        return new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            Log.Debug("CreateSectionSelectionCommand: Starting for publication={PublicationCode}, biblePublicationService={HasService}",
                x?.Code ?? "(null)", biblePublicationService != null);

            if (x == null)
            {
                Log.Warning("CreateSectionSelectionCommand: Publication is null, returning");
                return;
            }

            // Always use CurrentSchedule as the source of truth for language
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                Log.Warning("CreateSectionSelectionCommand: CurrentSchedule is null, returning");
                return;
            }

            var languageCode = currentSchedule.BiblePublicationLanguageCode;
            if (string.IsNullOrEmpty(languageCode))
            {
                Log.Warning("CreateSectionSelectionCommand: LanguageCode is empty, returning");
                return;
            }

            // Get language from the languages collection
            LanguageListViewItemModel currentLanguage;
            var languages = await Task.Run(async () => await mediaService.GetBiblePublicationLanguages());
            if (languages.TryGetValue(languageCode, out var language))
            {
                currentLanguage = new LanguageListViewItemModel(language);
            }
            else
            {
                // Create a minimal language item from the code if not found in collection
                currentLanguage = new LanguageListViewItemModel(new Language
                {
                    Id = 0,
                    Code = languageCode,
                    Name = languageCode
                });
            }

            Log.Debug("CreateSectionSelectionCommand: Calling GetSectionAndTrackForPublicationAsync for publication={PublicationCode}, language={LanguageCode}",
                x.Code, currentLanguage.Code);

            var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state, biblePublicationService);
            var (sectionNumber, trackNumber, sectionName, trackTitle) = await itemSelector.GetSectionAndTrackForPublicationAsync(x, currentLanguage);

            Log.Debug("CreateSectionSelectionCommand: Result sectionNumber={SectionNumber}, trackNumber={TrackNumber}, trackTitle={TrackTitle}",
                sectionNumber, trackNumber, trackTitle);

            // trackNumber must be valid; sectionNumber can be 0 for non-sectioned publications (dramas)
            if (trackNumber <= 0)
            {
                Log.Warning("CreateSectionSelectionCommand: Invalid trackNumber={TrackNumber}, returning", trackNumber);
                return;
            }

            var biblePublicationItem = CreateBiblePublicationItemFromSelection(x, sectionNumber, trackNumber, sectionName, trackTitle, currentLanguage, currentSchedule);

            Log.Information("CreateSectionSelectionCommand: Dispatching selection for publication={PublicationCode}, section={SectionNumber}, track={TrackNumber}",
                x.Code, sectionNumber, trackNumber);

            var actionDispatcher = new BiblePublicationSelectionActionDispatcher(dispatcher);
            actionDispatcher.DispatchBiblePublicationSelectionActions(biblePublicationItem);
            await navigationService.PopModalAsync();
        });
    }

    public ICommand CreateBackCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await navigationService.PopAsync();
        });
    }

    public ICommand CreateCloseModalCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });
    }

    public ICommand CreateSelectLanguageCommand(
        Func<ObservableCollection<LanguageListViewItemModel>> getLanguages,
        Func<Dictionary<string, PublicationListViewItemModel>> getPublicationVMsMapping,
        Action<LanguageListViewItemModel> updateSelectedLanguage)
    {
        return new AsyncRelayCommand<LanguageListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            updateSelectedLanguage(x);
            await navigationService.PopModalAsync();

            var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state, biblePublicationService);
            var (publicationCode, sectionNumber, trackNumber, sectionName, publicationName, trackTitle) =
                await itemSelector.GetPublicationSectionAndTrackForLanguageAsync(x);

            // Check for both null and empty string - GetPublicationSectionAndTrackForLanguageAsync returns empty string on failure
            if (string.IsNullOrEmpty(publicationCode))
            {
                Log.Warning("BibleSelectionCommandHandler: Cannot execute SelectLanguageCommand - No publications found for language {LanguageCode}", x.Code);
                return;
            }

            // Validate that we have valid track number (sectionNumber can be 0 for non-sectioned publications like dramas)
            if (trackNumber <= 0)
            {
                Log.Warning("BibleSelectionCommandHandler: Cannot execute SelectLanguageCommand - Invalid track ({TrackNumber}) for language {LanguageCode}", 
                    trackNumber, x.Code);
                return;
            }

            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                Log.Warning("BibleSelectionCommandHandler: Cannot execute SelectLanguageCommand - CurrentSchedule is null");
                return;
            }

            Log.Information("BibleSelectionCommandHandler: SelectLanguageCommand - Creating item for language {LanguageCode}, publication {PublicationCode}, section {SectionNumber}, track {TrackNumber}",
                x.Code, publicationCode, sectionNumber, trackNumber);

            var biblePublicationItem = CreateBiblePublicationItemForLanguageSelection(
                x, publicationCode, sectionNumber, trackNumber, sectionName, publicationName, trackTitle, currentSchedule);
            var actionDispatcher = new BiblePublicationSelectionActionDispatcher(dispatcher);
            actionDispatcher.DispatchLanguageSelectionActions(biblePublicationItem, x);
        });
    }

    private BiblePublicationStateItem CreateBiblePublicationItemFromSelection(
        PublicationListViewItemModel publication,
        int sectionNumber,
        int trackNumber,
        string sectionName,
        string trackTitle,
        LanguageListViewItemModel language,
        ScheduleStateItem currentSchedule)
    {
        // Match the pattern used in SectionSelectionViewModel and TrackSelectionCommandHandler
        // They don't set Id or AlarmScheduleId - let them default to 0
        return new BiblePublicationStateItem
        {
            PublicationCode = publication.Code,
            LanguageCode = language.Code,
            SectionNumber = sectionNumber,
            TrackNumber = trackNumber,
            LanguageName = language.Name,
            LanguageDirection = language.Direction,
            PublicationName = publication.Name,
            SectionName = sectionName,
            TrackTitle = trackTitle
        };
    }

    private BiblePublicationStateItem CreateBiblePublicationItemForLanguageSelection(
        LanguageListViewItemModel language,
        string publicationCode,
        int sectionNumber,
        int trackNumber,
        string sectionName,
        string publicationName,
        string trackTitle,
        ScheduleStateItem currentSchedule)
    {
        // Match the pattern used in SectionSelectionViewModel and TrackSelectionCommandHandler
        // They don't set Id or AlarmScheduleId - let them default to 0
        return new BiblePublicationStateItem
        {
            LanguageCode = language.Code,
            PublicationCode = publicationCode,
            SectionNumber = sectionNumber,
            TrackNumber = trackNumber,
            LanguageName = language.Name,
            LanguageDirection = language.Direction,
            PublicationName = publicationName,
            SectionName = sectionName,
            TrackTitle = trackTitle
        };
    }
}
