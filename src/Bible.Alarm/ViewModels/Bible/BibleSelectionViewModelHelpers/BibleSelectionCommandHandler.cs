#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for bible selection operations.
/// </summary>
public sealed class BibleSelectionCommandHandler
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IMapper mapper;

    public BibleSelectionCommandHandler(
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

    public ICommand CreateBookSelectionCommand(
        Func<LanguageListViewItemModel?> getCurrentLanguage,
        Func<ObservableCollection<PublicationListViewItemModel>> getTranslations,
        Func<Dictionary<string, PublicationListViewItemModel>> getTranslationVMsMapping,
        Func<BibleReadingSchedule?> getCurrent)
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

            var languageCode = currentSchedule.BibleReadingLanguageCode;
            if (string.IsNullOrEmpty(languageCode))
            {
                return;
            }

            // Get language from the languages collection
            LanguageListViewItemModel currentLanguage;
            var languages = await Task.Run(async () => await mediaService.GetBibleLanguages());
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

            var itemSelector = new BibleSelectionItemSelector(mediaService, state);
            var (bookNumber, chapterNumber, bookName) = await itemSelector.GetBookAndChapterForTranslationAsync(x, currentLanguage);
            
            if (bookNumber == 0)
            {
                return;
            }

            var bibleReadingItem = CreateBibleReadingItemFromSelection(x, bookNumber, chapterNumber, bookName, currentLanguage, currentSchedule);
            
            var actionDispatcher = new BibleSelectionActionDispatcher(dispatcher);
            actionDispatcher.DispatchBibleReadingSelectionActions(bibleReadingItem);
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

            var itemSelector = new BibleSelectionItemSelector(mediaService, state);
            var (publicationCode, bookNumber, chapterNumber, bookName, publicationName) =
                await itemSelector.GetTranslationBookAndChapterForLanguageAsync(x);

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

            var bibleReadingItem = CreateBibleReadingItemForLanguageSelection(
                x, publicationCode, bookNumber, chapterNumber, bookName, publicationName, currentSchedule);
            var actionDispatcher = new BibleSelectionActionDispatcher(dispatcher);
            actionDispatcher.DispatchLanguageSelectionActions(bibleReadingItem, x);
        });
    }

    private BibleReadingStateItem CreateBibleReadingItemFromSelection(
        PublicationListViewItemModel publication,
        int bookNumber,
        int chapterNumber,
        string bookName,
        LanguageListViewItemModel language,
        ScheduleStateItem currentSchedule)
    {
        // Match the pattern used in BookSelectionViewModel and ChapterSelectionCommandHandler
        // They don't set Id or AlarmScheduleId - let them default to 0
        return new BibleReadingStateItem
        {
            PublicationCode = publication.Code,
            LanguageCode = language.Code,
            BookNumber = bookNumber,
            ChapterNumber = chapterNumber,
            LanguageName = language.Name,
            PublicationName = publication.Name,
            BookName = bookName
        };
    }

    private BibleReadingStateItem CreateBibleReadingItemForLanguageSelection(
        LanguageListViewItemModel language,
        string publicationCode,
        int bookNumber,
        int chapterNumber,
        string bookName,
        string publicationName,
        ScheduleStateItem currentSchedule)
    {
        // Match the pattern used in BookSelectionViewModel and ChapterSelectionCommandHandler
        // They don't set Id or AlarmScheduleId - let them default to 0
        return new BibleReadingStateItem
        {
            LanguageCode = language.Code,
            PublicationCode = publicationCode,
            BookNumber = bookNumber,
            ChapterNumber = chapterNumber,
            LanguageName = language.Name,
            PublicationName = publicationName,
            BookName = bookName
        };
    }
}
