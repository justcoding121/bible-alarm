#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
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
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    public BiblePublicationSelectionCommandHandler(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IMapper mapper)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.mapper = mapper;
    }

    public ICommand CreateSectionSelectionCommand(
        Func<LanguageListViewItemModel?> getCurrentLanguage,
        Func<ObservableCollection<PublicationListViewItemModel>> getTranslations,
        Func<Dictionary<string, PublicationListViewItemModel>> getTranslationVMsMapping,
        Func<BiblePublicationSchedule?> getCurrent)
    {
        return new AsyncRelayCommand<PublicationListViewItemModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            // Always use CurrentSchedule as the source of truth for language
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return;
            }

            var languageCode = currentSchedule.BiblePublicationLanguageCode;
            if (string.IsNullOrEmpty(languageCode))
            {
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

            var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state);
            var (sectionNumber, trackNumber, sectionName) = await itemSelector.GetSectionAndTrackForTranslationAsync(x, currentLanguage);

            if (sectionNumber == 0)
            {
                return;
            }

            var biblePublicationItem = CreateBiblePublicationItemFromSelection(x, sectionNumber, trackNumber, sectionName, currentLanguage, currentSchedule);

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
        Func<Dictionary<string, PublicationListViewItemModel>> getTranslationVMsMapping,
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

            var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state);
            var (publicationCode, sectionNumber, trackNumber, sectionName, publicationName) =
                await itemSelector.GetTranslationSectionAndTrackForLanguageAsync(x);

            if (publicationCode == null)
            {
                return;
            }

            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                Log.Warning("BibleSelectionCommandHandler: Cannot execute SelectLanguageCommand - CurrentSchedule is null");
                return;
            }

            var biblePublicationItem = CreateBiblePublicationItemForLanguageSelection(
                x, publicationCode, sectionNumber, trackNumber, sectionName, publicationName, currentSchedule);
            var actionDispatcher = new BiblePublicationSelectionActionDispatcher(dispatcher);
            actionDispatcher.DispatchLanguageSelectionActions(biblePublicationItem, x);
        });
    }

    private BiblePublicationStateItem CreateBiblePublicationItemFromSelection(
        PublicationListViewItemModel publication,
        int sectionNumber,
        int trackNumber,
        string sectionName,
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
            PublicationName = publication.Name,
            SectionName = sectionName
        };
    }

    private BiblePublicationStateItem CreateBiblePublicationItemForLanguageSelection(
        LanguageListViewItemModel language,
        string publicationCode,
        int sectionNumber,
        int trackNumber,
        string sectionName,
        string publicationName,
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
            PublicationName = publicationName,
            SectionName = sectionName
        };
    }
}
