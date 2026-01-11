#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles data population for bible selection (languages and translations).
/// </summary>
public sealed class BiblePublicationSelectionDataProvider
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;

    public readonly Dictionary<string, PublicationListViewItemModel> translationVMsMapping = [];

    public BiblePublicationSelectionDataProvider(
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
        if (languages == null) return;

        // Capture current language code before Task.Run to avoid state access issues
        var currentLanguageCode = state.Value.CurrentSchedule?.BiblePublicationLanguageCode;

        // Do ALL processing on background thread to avoid blocking spinner animation
        var languageVMs = await Task.Run(async () =>
        {
            var languagesData = await mediaService.GetBiblePublicationLanguages();
            var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

            var vms = new List<LanguageListViewItemModel>();

            foreach (var language in languagesData.Values
                         .Where(x => trimmedSearchTerm == null
                                     || x.Name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.Name))
            {
                var languageVm = new LanguageListViewItemModel(language);
                vms.Add(languageVm);

                if (!string.IsNullOrEmpty(currentLanguageCode) && languageVm.Code == currentLanguageCode)
                {
                    languageVm.IsSelected = true;
                }
            }

            return vms;
        });

        // Add items in small batches with frequent yields for smooth spinner animation
        const int batchSize = 15;
        await MainThread.InvokeOnMainThreadAsync(() => languages.Clear());
        await Task.Yield(); // Let spinner animate after clear

        for (int i = 0; i < languageVMs.Count; i += batchSize)
        {
            var batch = languageVMs.Skip(i).Take(batchSize).ToList();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var lang in batch)
                {
                    languages.Add(lang);
                }
            });

            // Yield after every batch for smooth animation
            await Task.Yield();
        }
    }

    public async Task PopulateTranslationsAsync(
        string languageCode,
        ObservableCollection<PublicationListViewItemModel>? translations,
        bool languageChanged)
    {
        if (translations == null) return;

        // Capture state values before Task.Run to avoid state access issues
        var stateValue = state.Value;
        var currentPublicationCode = stateValue.CurrentSchedule?.BiblePublicationCode;
        var currentLanguageName = stateValue.CurrentSchedule?.BiblePublicationLanguageName;

        // Do ALL processing on background thread to avoid blocking spinner animation
        var (translationVMs, newMapping, defaultTranslation) = await Task.Run(async () =>
        {
            var translationsData = await mediaService.GetBiblePublications(languageCode);
            var vms = new List<PublicationListViewItemModel>();
            var mapping = new Dictionary<string, PublicationListViewItemModel>();
            PublicationListViewItemModel? lastTranslation = null;

            foreach (var translation in translationsData.Values)
            {
                // Skip duplicates - if code already exists, use the existing one
                if (mapping.TryGetValue(translation.Code, out var existingVm))
                {
                    if (!string.IsNullOrEmpty(currentPublicationCode) && currentPublicationCode == translation.Code)
                    {
                        existingVm.IsSelected = true;
                    }
                    continue;
                }

                var translationVm = new PublicationListViewItemModel(translation);
                vms.Add(translationVm);
                mapping[translationVm.Code] = translationVm;

                // Store the last translation as default
                lastTranslation = translationVm;

                // Check if this translation matches the current publication code
                if (!string.IsNullOrEmpty(currentPublicationCode) && currentPublicationCode == translation.Code)
                {
                    translationVm.IsSelected = true;
                }
            }

            return (vms, mapping, lastTranslation);
        });

        // Update mapping
        translationVMsMapping.Clear();
        foreach (var kvp in newMapping)
        {
            translationVMsMapping[kvp.Key] = kvp.Value;
        }

        // Handle language change default selection
        if (languageChanged && defaultTranslation != null)
        {
            defaultTranslation.IsSelected = true;

            // Check if state already matches what we're about to dispatch
            var currentSchedule = state.Value.CurrentSchedule;
            var alreadyMatches = currentSchedule != null &&
                                currentSchedule.BiblePublicationLanguageCode == languageCode &&
                                currentSchedule.BiblePublicationCode == defaultTranslation.Code &&
                                currentSchedule.BiblePublicationSectionNumber == 1 &&
                                currentSchedule.BiblePublicationTrackNumber == 1;

            if (!alreadyMatches)
            {
                // Fire and forget the dispatch (already on background thread)
                _ = DispatchDefaultTranslationAsync(languageCode, defaultTranslation, currentLanguageName);
            }
        }

        // Minimal UI thread work - just swap the collection contents
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            translations.Clear();
            foreach (var trans in translationVMs)
            {
                translations.Add(trans);
            }
        });
    }

    private async Task DispatchDefaultTranslationAsync(
        string languageCode,
        PublicationListViewItemModel defaultTranslation,
        string? languageName)
    {
        try
        {
            var sections = await mediaService.GetBiblePublicationSections(languageCode, defaultTranslation.Code);
            if (sections == null || sections.Count == 0) return;

            var firstSection = sections.Values.First();
            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, defaultTranslation.Code, firstSection.Number);
            if (tracks == null || tracks.Count == 0) return;

            var firstTrack = tracks.Values.First();
            var biblePublicationItem = new BiblePublicationStateItem
            {
                LanguageCode = languageCode,
                PublicationCode = defaultTranslation.Code,
                SectionNumber = firstSection.Number,
                TrackNumber = firstTrack.Number,
                LanguageName = languageName,
                PublicationName = defaultTranslation.Name,
                SectionName = firstSection.Name
            };
            dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "BibleSelectionDataProvider: Error dispatching default translation selection");
        }
    }

    public Dictionary<string, PublicationListViewItemModel> GetTranslationVMsMapping()
    {
        return translationVMsMapping;
    }
}
