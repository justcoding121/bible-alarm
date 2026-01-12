#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles data population for bible selection (languages and publications).
/// </summary>
public sealed class BiblePublicationSelectionDataProvider
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly SemaphoreSlim languagePopulationLock = new(1, 1);

    public readonly Dictionary<string, PublicationListViewItemModel> publicationVMsMapping = [];

    public BiblePublicationSelectionDataProvider(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.dispatcher = dispatcher;
    }

    public void ClearPublicationVMsMapping()
    {
        publicationVMsMapping.Clear();
    }

    public async Task PopulateLanguagesAsync(
        string? searchTerm,
        ObservableCollection<LanguageListViewItemModel>? languages)
    {
        if (languages == null) return;

        // Prevent concurrent population which can cause duplicates
        await languagePopulationLock.WaitAsync();
        try
        {
            // Capture current language code before Task.Run to avoid state access issues
            var currentLanguageCode = state.Value.CurrentSchedule?.BiblePublicationLanguageCode;

            // Do ALL processing on background thread to avoid blocking spinner animation
            var languageVMs = await Task.Run(async () =>
            {
                var languagesData = await mediaService.GetBiblePublicationLanguages();
                var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

                Log.Debug("PopulateLanguagesAsync: Loaded {LanguageCount} languages from GetBiblePublicationLanguages: {LanguageCodes}",
                    languagesData.Count, string.Join(", ", languagesData.Keys));

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

                Log.Debug("PopulateLanguagesAsync: Created {VMCount} language VMs: {LanguageNames}",
                    vms.Count, string.Join(", ", vms.Select(v => $"{v.Code}:{v.Name}")));

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
        finally
        {
            languagePopulationLock.Release();
        }
    }

    public async Task PopulatePublicationsAsync(
        string languageCode,
        ObservableCollection<PublicationListViewItemModel>? publications,
        bool languageChanged)
    {
        if (publications == null) return;

        // Capture state values before Task.Run to avoid state access issues
        var stateValue = state.Value;
        var currentPublicationCode = stateValue.CurrentSchedule?.BiblePublicationCode;
        var currentLanguageName = stateValue.CurrentSchedule?.BiblePublicationLanguageName;
        var currentLanguageDirection = stateValue.CurrentSchedule?.BiblePublicationLanguageDirection;

        // Do ALL processing on background thread to avoid blocking spinner animation
        var (publicationVMs, newMapping, defaultPublication) = await Task.Run(async () =>
        {
            var publicationsData = await mediaService.GetBiblePublications(languageCode);
            var vms = new List<PublicationListViewItemModel>();
            var mapping = new Dictionary<string, PublicationListViewItemModel>();
            PublicationListViewItemModel? lastPublication = null;

            foreach (var publication in publicationsData.Values)
            {
                // Skip duplicates - if code already exists, use the existing one
                if (mapping.TryGetValue(publication.Code, out var existingVm))
                {
                    if (!string.IsNullOrEmpty(currentPublicationCode) && currentPublicationCode == publication.Code)
                    {
                        existingVm.IsSelected = true;
                    }
                    continue;
                }

                var publicationVm = new PublicationListViewItemModel(publication);
                vms.Add(publicationVm);
                mapping[publicationVm.Code] = publicationVm;

                // Store the last publication as default
                lastPublication = publicationVm;

                // Check if this publication matches the current publication code
                if (!string.IsNullOrEmpty(currentPublicationCode) && currentPublicationCode == publication.Code)
                {
                    publicationVm.IsSelected = true;
                }
            }

            return (vms, mapping, lastPublication);
        });

        // Update mapping
        publicationVMsMapping.Clear();
        foreach (var kvp in newMapping)
        {
            publicationVMsMapping[kvp.Key] = kvp.Value;
        }

        // Handle language change default selection
        if (languageChanged && defaultPublication != null)
        {
            defaultPublication.IsSelected = true;

            // Check if state already matches what we're about to dispatch
            var currentSchedule = state.Value.CurrentSchedule;
            var alreadyMatches = currentSchedule != null &&
                                currentSchedule.BiblePublicationLanguageCode == languageCode &&
                                currentSchedule.BiblePublicationCode == defaultPublication.Code &&
                                currentSchedule.BiblePublicationSectionNumber == 1 &&
                                currentSchedule.BiblePublicationTrackNumber == 1;

            if (!alreadyMatches)
            {
                // Fire and forget the dispatch (already on background thread)
                _ = DispatchDefaultPublicationAsync(languageCode, defaultPublication, currentLanguageName, currentLanguageDirection);
            }
        }

        // Minimal UI thread work - just swap the collection contents
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            publications.Clear();
            foreach (var trans in publicationVMs)
            {
                publications.Add(trans);
            }
        });
    }

    private async Task DispatchDefaultPublicationAsync(
        string languageCode,
        PublicationListViewItemModel defaultPublication,
        string? languageName,
        string? languageDirection)
    {
        try
        {
            var sections = await mediaService.GetBiblePublicationSections(languageCode, defaultPublication.Code);
            if (sections == null || sections.Count == 0) return;

            var firstSection = sections.Values.First();
            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, defaultPublication.Code, firstSection.Number);
            if (tracks == null || tracks.Count == 0) return;

            var firstTrack = tracks.Values.First();
            var biblePublicationItem = new BiblePublicationStateItem
            {
                LanguageCode = languageCode,
                PublicationCode = defaultPublication.Code,
                SectionNumber = firstSection.Number,
                TrackNumber = firstTrack.Number,
                LanguageName = languageName,
                LanguageDirection = languageDirection,
                PublicationName = defaultPublication.Name,
                SectionName = firstSection.Name,
                TrackTitle = firstTrack.Title
            };
            dispatcher.Dispatch(new TrackSelectedAction(biblePublicationItem));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "BibleSelectionDataProvider: Error dispatching default publication selection");
        }
    }

    public Dictionary<string, PublicationListViewItemModel> GetPublicationVMsMapping()
    {
        return publicationVMsMapping;
    }
}
