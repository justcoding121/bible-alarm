#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Bible.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles data population for bible selection (languages and translations).
/// </summary>
public sealed class BibleSelectionDataProvider
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;

    public readonly Dictionary<string, PublicationListViewItemModel> translationVMsMapping = [];

    public BibleSelectionDataProvider(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
    }

    public void ClearTranslationVMsMapping()
    {
        translationVMsMapping.Clear();
    }

    public async Task PopulateLanguagesAsync(
        string? searchTerm,
        ObservableCollection<LanguageListViewItemModel>? languages)
    {
        // Run database operations off UI thread
        var languagesData = await Task.Run(async () =>
            await mediaService.GetBibleLanguages());

        var languageVMs = new ObservableCollection<LanguageListViewItemModel>();

        // Trim the search term before using it
        var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

        foreach (var language in languagesData.Select(x => x.Value)
                     .Where(x => trimmedSearchTerm == null
                                 || x.Name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Name))
        {
            var languageVm = new LanguageListViewItemModel(language);

            languageVMs.Add(languageVm);

            // Use CurrentSchedule as the source of truth for language code
            var stateValue = state.Value;
            var currentLanguageCode = stateValue.CurrentSchedule?.BibleReadingLanguageCode;

            if (string.IsNullOrEmpty(currentLanguageCode) || languageVm.Code != currentLanguageCode)
            {
                continue;
            }

            languageVm.IsSelected = true;
        }

        // Assign collection on main thread to ensure UI updates
        if (languages != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                languages.Clear();
                foreach (var lang in languageVMs)
                {
                    languages.Add(lang);
                }
            });
        }
    }

    public async Task PopulateTranslationsAsync(
        string languageCode,
        ObservableCollection<PublicationListViewItemModel>? translations,
        bool languageChanged)
    {
        translationVMsMapping.Clear();

        // Run database operations off UI thread
        var translationsData = await Task.Run(async () =>
            await mediaService.GetBibleTranslations(languageCode));
        var translationVMs = new ObservableCollection<PublicationListViewItemModel>();

        // Use CurrentSchedule as the source of truth for publication code
        var stateValue = state.Value;
        var currentPublicationCode = stateValue.CurrentSchedule?.BibleReadingPublicationCode;

        PublicationListViewItemModel? defaultTranslation = null;

        foreach (var translation in translationsData.Select(x => x.Value))
        {
            // Skip duplicates - if code already exists, use the existing one
            if (translationVMsMapping.TryGetValue(translation.Code, out var existingVm))
            {
                // Still check if this duplicate matches the current publication code
                if (!string.IsNullOrEmpty(currentPublicationCode)
                    && currentPublicationCode == translation.Code)
                {
                    existingVm.IsSelected = true;
                }
                continue;
            }

            var translationVm = new PublicationListViewItemModel(translation);

            translationVMs.Add(translationVm);
            translationVMsMapping[translationVm.Code] = translationVm;

            // Store the last translation (reverse order) as default
            defaultTranslation = translationVm;

            // Check if this translation matches the current publication code from CurrentSchedule
            if (!string.IsNullOrEmpty(currentPublicationCode)
                && currentPublicationCode == translation.Code)
            {
                translationVm.IsSelected = true;
            }
        }

        // If language changed and no translation matches current publication code, select the last one (reverse order)
        // Only dispatch action if the state doesn't already match what we're about to set
        if (languageChanged && defaultTranslation != null)
        {
            defaultTranslation.IsSelected = true;

            // Check if state already matches what we're about to dispatch to avoid duplicate dispatches
            var currentState = state.Value;
            var currentSchedule = currentState?.CurrentSchedule;
            var alreadyMatches = currentSchedule != null &&
                                currentSchedule.BibleReadingLanguageCode == languageCode &&
                                currentSchedule.BibleReadingPublicationCode == defaultTranslation.Code &&
                                currentSchedule.BibleReadingBookNumber == 1 &&
                                currentSchedule.BibleReadingChapterNumber == 1;

            // Only dispatch if state doesn't already match (prevents duplicate dispatches from RefreshFromState)
            if (!alreadyMatches)
            {
                // Dispatch action to update state with the default translation
                // Get the first book and chapter for the default translation
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var books = await mediaService.GetBibleBooks(languageCode, defaultTranslation.Code);
                        if (books != null && books.Count > 0)
                        {
                            var firstBook = books.Values.First();
                            var chapters = await mediaService.GetBibleChapters(languageCode, defaultTranslation.Code, firstBook.Number);
                            if (chapters != null && chapters.Count > 0)
                            {
                                var firstChapter = chapters.Values.First();
                                var bibleReadingItem = new BibleReadingStateItem
                                {
                                    LanguageCode = languageCode,
                                    PublicationCode = defaultTranslation.Code,
                                    BookNumber = firstBook.Number,
                                    ChapterNumber = firstChapter.Number,
                                    LanguageName = stateValue.CurrentSchedule?.BibleReadingLanguageName,
                                    PublicationName = defaultTranslation.Name,
                                    BookName = firstBook.Name
                                };
                                dispatcher.Dispatch(new ChapterSelectedAction(bibleReadingItem));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "BibleSelectionDataProvider: Error dispatching default translation selection");
                    }
                });
            }
        }

        // Assign collection on main thread to ensure UI updates before IsBusy is set to false
        if (translations != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                translations.Clear();
                foreach (var trans in translationVMs)
                {
                    translations.Add(trans);
                }
            });
        }
    }

    public Dictionary<string, PublicationListViewItemModel> GetTranslationVMsMapping()
    {
        return translationVMsMapping;
    }
}
