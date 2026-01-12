#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles cascade selection logic for Bible publications.
/// Cascade order: Language → Publication → Section → Track
/// </summary>
public sealed class BiblePublicationSelectionItemSelector
{
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly IState<ApplicationState> state;

    // Priority codes for Bible publications: nwt (2013 NWT), bi12 (1984 NWT)
    private static readonly string[] PriorityPublicationCodes = ["nwt", "bi12"];

    public BiblePublicationSelectionItemSelector(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IBiblePublicationService? biblePublicationService = null)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.biblePublicationService = biblePublicationService;
    }

    /// <summary>
    /// When user selects a publication, cascade to get first section and track.
    /// Dynamically detects if publication has sections by querying the database.
    /// </summary>
    public async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)> 
        GetSectionAndTrackForPublicationAsync(
            PublicationListViewItemModel publication,
            LanguageListViewItemModel language)
    {
        Log.Debug("GetSectionAndTrackForPublicationAsync: Starting for publication={PublicationCode}, language={LanguageCode}",
            publication.Code, language.Code);

        // First, try to get sections from the database
        var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(language.Code, publication.Code));

        // If publication has sections, use sectioned flow
        if (sections != null && sections.Count > 0)
        {
            Log.Debug("GetSectionAndTrackForPublicationAsync: Found {SectionCount} sections, using sectioned flow",
                sections.Count);
            var sectionedResult = await GetFirstSectionAndTrackFromSectionsAsync(language.Code, publication.Code, sections);
            Log.Debug("GetSectionAndTrackForPublicationAsync: Sectioned result: sectionNumber={SectionNumber}, trackNumber={TrackNumber}, sectionName={SectionName}, trackTitle={TrackTitle}",
                sectionedResult.SectionNumber, sectionedResult.TrackNumber, sectionedResult.SectionName, sectionedResult.TrackTitle);
            
            // Warn if names are empty but numbers are valid
            if (sectionedResult.SectionNumber > 0 && string.IsNullOrWhiteSpace(sectionedResult.SectionName))
            {
                Log.Warning("GetSectionAndTrackForPublicationAsync: SectionName is empty for sectionNumber={SectionNumber}", 
                    sectionedResult.SectionNumber);
            }
            if (sectionedResult.TrackNumber > 0 && string.IsNullOrWhiteSpace(sectionedResult.TrackTitle))
            {
                Log.Warning("GetSectionAndTrackForPublicationAsync: TrackTitle is empty for trackNumber={TrackNumber}", 
                    sectionedResult.TrackNumber);
            }
            
            return sectionedResult;
        }

        // No sections found - this is a non-sectioned publication (drama/video)
        Log.Debug("GetSectionAndTrackForPublicationAsync: No sections found, using non-sectioned flow");
        var result = await GetFirstTrackForNonSectionedAsync(language.Code, publication.Code);
        Log.Debug("GetSectionAndTrackForPublicationAsync: Non-sectioned result: trackNumber={TrackNumber}, trackTitle={TrackTitle}",
            result.TrackNumber, result.TrackTitle);
        return result;
    }

    /// <summary>
    /// When user selects a language, cascade to get first publication, section, and track.
    /// Dynamically detects if publication has sections by querying the database.
    /// </summary>
    public async Task<(string? PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetPublicationSectionAndTrackForLanguageAsync(LanguageListViewItemModel language)
    {
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Starting for language={LanguageCode}", language.Code);

        // Get all publications for this language
        var publications = await Task.Run(async () =>
            await mediaService.GetBiblePublications(language.Code));

        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Found {PublicationCount} publications for language={LanguageCode}",
            publications?.Count ?? 0, language.Code);

        if (publications == null || publications.Count == 0)
        {
            Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: No publications found for language={LanguageCode}", language.Code);
            return (null, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        // Select preferred publication: nwt first, then bi12, then first available
        string publicationCode;
        string publicationName;
        KeyValuePair<string, BiblePublication>? preferredPublication = null;

        foreach (var priorityCode in PriorityPublicationCodes)
        {
            if (publications.TryGetValue(priorityCode, out var pub))
            {
                preferredPublication = new KeyValuePair<string, BiblePublication>(priorityCode, pub);
                break;
            }
        }

        // Fall back to first publication if no priority publications found
        var selectedPublication = preferredPublication ?? publications.First();
        publicationCode = selectedPublication.Key;
        publicationName = selectedPublication.Value.Name;

        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Selected publication code={PublicationCode}, name={PublicationName}",
            publicationCode, publicationName);

        // Try to get sections to determine if publication is sectioned or not
        var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(language.Code, publicationCode));

        if (sections != null && sections.Count > 0)
        {
            // Sectioned publication - get first section and track
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Found {SectionCount} sections, using sectioned flow", sections.Count);
            var (sectionNumber, firstTrackNumber, sectionName, firstTrackTitle) = 
                await GetFirstSectionAndTrackFromSectionsAsync(language.Code, publicationCode, sections);

            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Sectioned result: sectionNumber={SectionNumber}, trackNumber={TrackNumber}",
                sectionNumber, firstTrackNumber);

            return (publicationCode, sectionNumber, firstTrackNumber, sectionName, publicationName, firstTrackTitle);
        }

        // Non-sectioned publication (drama/video) - get first track directly
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: No sections found, using non-sectioned flow");
        var (_, trackNumber, _, trackTitle) = await GetFirstTrackForNonSectionedAsync(language.Code, publicationCode);
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Non-sectioned result: trackNumber={TrackNumber}, trackTitle={TrackTitle}",
            trackNumber, trackTitle);
        return (publicationCode, 0, trackNumber, string.Empty, publicationName, trackTitle);
    }

    /// <summary>
    /// Gets first section and first track for a sectioned publication.
    /// </summary>
    private async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)>
        GetFirstSectionAndTrackAsync(string languageCode, string publicationCode)
    {
        var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(languageCode, publicationCode));

        if (sections == null || sections.Count == 0)
        {
            return (0, 0, string.Empty, string.Empty);
        }

        return await GetFirstSectionAndTrackFromSectionsAsync(languageCode, publicationCode, sections);
    }

    /// <summary>
    /// Gets first section and first track from pre-loaded sections.
    /// </summary>
    private async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)>
        GetFirstSectionAndTrackFromSectionsAsync(string languageCode, string publicationCode, SortedDictionary<int, BiblePublicationSection> sections)
    {
        var firstSection = sections.Values.First();
        Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: First section number={SectionNumber}, name={SectionName}",
            firstSection.Number, firstSection.Name);

        var tracks = await Task.Run(async () =>
            await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, firstSection.Number));

        if (tracks == null || tracks.Count == 0)
        {
            Log.Warning("GetFirstSectionAndTrackFromSectionsAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}, section={SectionNumber}",
                languageCode, publicationCode, firstSection.Number);
            return (0, 0, string.Empty, string.Empty);
        }

        var firstTrack = tracks.Values.First();
        Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: First track number={TrackNumber}, title={TrackTitle}",
            firstTrack.Number, firstTrack.Title);
        return (firstSection.Number, firstTrack.Number, firstSection.Name, firstTrack.Title);
    }

    /// <summary>
    /// Gets first track for a non-sectioned publication (drama/video).
    /// </summary>
    private async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)>
        GetFirstTrackForNonSectionedAsync(string languageCode, string publicationCode)
    {
        Log.Debug("GetFirstTrackForNonSectionedAsync: Starting for language={LanguageCode}, publication={PublicationCode}, biblePublicationService={HasService}",
            languageCode, publicationCode, biblePublicationService != null);

        if (biblePublicationService == null)
        {
            Log.Warning("GetFirstTrackForNonSectionedAsync: biblePublicationService is null, returning empty result");
            return (0, 0, string.Empty, string.Empty);
        }

        var publication = await Task.Run(async () =>
            await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode));

        Log.Debug("GetFirstTrackForNonSectionedAsync: Loaded publication={PublicationName}, TracksCount={TracksCount}",
            publication?.Name ?? "(null)", publication?.Tracks?.Count ?? 0);

        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            Log.Warning("GetFirstTrackForNonSectionedAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}",
                languageCode, publicationCode);
            return (0, 0, string.Empty, string.Empty);
        }

        var firstTrack = publication.Tracks.OrderBy(t => t.Number).First();
        Log.Information("GetFirstTrackForNonSectionedAsync: Found first track Number={TrackNumber}, Title={TrackTitle}",
            firstTrack.Number, firstTrack.Title);
        return (0, firstTrack.Number, string.Empty, firstTrack.Title);
    }
}
