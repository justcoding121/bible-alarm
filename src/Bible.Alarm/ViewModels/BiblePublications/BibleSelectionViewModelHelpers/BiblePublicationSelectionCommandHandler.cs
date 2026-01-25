#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly ILanguageContentService? languageContentService;
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
        IBiblePublicationService? biblePublicationService = null,
        ILanguageContentService? languageContentService = null)
    {
        this.mediaService = mediaService;
        this.biblePublicationService = biblePublicationService;
        this.languageContentService = languageContentService;
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
            
            // If language code is empty, try to determine it from the selected publication
            // This handles the case when switching from a publication without language to one with language
            // IMPORTANT: If the publication doesn't have a language (LanguageId == null), we should still proceed
            // The current language code will remain empty/null, and the publication will be queried without language
            if (string.IsNullOrEmpty(languageCode))
            {
                Log.Debug("CreateSectionSelectionCommand: LanguageCode is empty, attempting to determine language from publication={PublicationCode}",
                    x.Code);
                
                // Query the database to find the language for this publication
                var langScopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
                if (langScopeFactory != null)
                {
                    using var langScope = langScopeFactory.CreateScope();
                    var db = langScope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
                    var publication = await db.BiblePublications
                        .AsNoTracking()
                        .Include(bp => bp.Language)
                        .Where(bp => bp.PublicationCode == x.Code && bp.LanguageId != null)
                        .FirstOrDefaultAsync();
                    
                    if (publication?.Language != null)
                    {
                        languageCode = publication.Language.LanguageCode;
                        Log.Debug("CreateSectionSelectionCommand: Determined language={LanguageCode} from publication={PublicationCode}",
                            languageCode, x.Code);
                    }
                    else
                    {
                        // Publication doesn't have a language (LanguageId == null) - this is valid
                        // We'll proceed with languageCode = null/empty, and the itemSelector will handle it
                        Log.Debug("CreateSectionSelectionCommand: Publication={PublicationCode} does not have LanguageId (publication without language), proceeding with empty language code",
                            x.Code);
                    }
                }
                else
                {
                    Log.Warning("CreateSectionSelectionCommand: LanguageCode is empty and IServiceScopeFactory is not available, but proceeding anyway");
                }
            }

            // Get language from the languages collection
            // If languageCode is empty/null, create a minimal language item (for publications without language)
            LanguageListViewItemModel currentLanguage;
            if (string.IsNullOrEmpty(languageCode))
            {
                // Publication doesn't have a language - create a minimal language item with empty code
                // The itemSelector will handle this correctly
                currentLanguage = new LanguageListViewItemModel(new Language
                {
                    Id = 0,
                    LanguageCode = string.Empty,
                    Name = string.Empty
                });
            }
            else
            {
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
                        LanguageCode = languageCode,
                        Name = languageCode
                    });
                }
            }

            Log.Debug("CreateSectionSelectionCommand: Calling GetSectionAndTrackForPublicationAsync for publication={PublicationCode}, language={LanguageCode}",
                x.Code, currentLanguage.Code);

            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
            var biblePublicationSectionService = ServiceProviderManager.GetService<IBiblePublicationSectionService>();
            var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state, biblePublicationService, biblePublicationSectionService, languageContentService, scopeFactory);
            var (sectionNumber, trackNumber, sectionName, trackTitle) = await itemSelector.GetSectionAndTrackForPublicationAsync(x, currentLanguage);

            Log.Debug("CreateSectionSelectionCommand: Result sectionNumber={SectionNumber}, trackNumber={TrackNumber}, sectionName={SectionName}, trackTitle={TrackTitle}",
                sectionNumber, trackNumber, sectionName, trackTitle);

            // trackNumber must be valid; sectionNumber can be 0 for non-sectioned publications (dramas)
            if (trackNumber <= 0)
            {
                Log.Warning("CreateSectionSelectionCommand: Invalid trackNumber={TrackNumber}, returning", trackNumber);
                return;
            }

            // Warn if names are empty - this could cause empty rows in the UI
            if (sectionNumber > 0 && string.IsNullOrWhiteSpace(sectionName))
            {
                Log.Warning("CreateSectionSelectionCommand: SectionName is empty for sectionNumber={SectionNumber}, publication={PublicationCode}. This may cause empty section row in UI.",
                    sectionNumber, x.Code);
            }
            if (string.IsNullOrWhiteSpace(trackTitle))
            {
                Log.Warning("CreateSectionSelectionCommand: TrackTitle is empty for trackNumber={TrackNumber}, publication={PublicationCode}. This may cause empty track row in UI.",
                    trackNumber, x.Code);
            }

            var biblePublicationItem = CreateBiblePublicationItemFromSelection(x, sectionNumber, trackNumber, sectionName, trackTitle, currentLanguage, currentSchedule);

            Log.Information("CreateSectionSelectionCommand: Dispatching selection for publication={PublicationCode}, section={SectionNumber}, track={TrackNumber}, sectionName={SectionName}, trackTitle={TrackTitle}",
                x.Code, sectionNumber, trackNumber, sectionName, trackTitle);

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

            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
            var biblePublicationSectionService = ServiceProviderManager.GetService<IBiblePublicationSectionService>();
            var itemSelector = new BiblePublicationSelectionItemSelector(mediaService, state, biblePublicationService, biblePublicationSectionService, languageContentService, scopeFactory);
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
            actionDispatcher.DispatchLanguageSelectionActions(biblePublicationItem);
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
        // IMPORTANT: Always preserve category from current schedule - category can only be changed via CategorySelectionAction
        // Category should NEVER be null in current schedule - if it is, that's a bug that needs to be fixed at the source
        var categoryId = currentSchedule.BiblePublicationCategoryId;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            // Category is null in current schedule - this should NEVER happen
            // Category can only be changed via CategorySelectionAction and should always be preserved
            Log.Error("CreateBiblePublicationItemFromSelection: Category is null in current schedule. This is a bug - category must always be selected. Publication={PublicationCode}, ScheduleId={ScheduleId}",
                publication.Code, currentSchedule.Id);
        }
        
        return new BiblePublicationStateItem
        {
            CategoryId = categoryId,
            CategoryName = categoryName,
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
