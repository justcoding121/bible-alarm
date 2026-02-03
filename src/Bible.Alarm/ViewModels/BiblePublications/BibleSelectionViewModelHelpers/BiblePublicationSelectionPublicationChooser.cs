#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

internal sealed class BiblePublicationSelectionPublicationChooser
{
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly ILanguageContentService? languageContentService;
    private readonly BiblePublicationSelectionSectionTrackResolver sectionTrackResolver;

    public BiblePublicationSelectionPublicationChooser(
        IBiblePublicationService? biblePublicationService,
        ILanguageContentService? languageContentService,
        BiblePublicationSelectionSectionTrackResolver sectionTrackResolver)
    {
        this.biblePublicationService = biblePublicationService;
        this.languageContentService = languageContentService;
        this.sectionTrackResolver = sectionTrackResolver;
    }

    internal async Task<(string? PublicationCode, BiblePublication? Publication, bool PublicationWithoutLanguage)> ChooseAsync(
        Dictionary<string, BiblePublication> publications,
        LanguageListViewItemModel language,
        IFetchProgress? progress = null)
    {
        if (publications.Count == 0)
        {
            return (null, null, false);
        }

        if (biblePublicationService == null)
        {
            var firstPub = publications.First();
            return (firstPub.Key, firstPub.Value, firstPub.Value.LanguageId == null);
        }

        // Iterate through publications in priority order: nwt first, then bi12, then others
        foreach (var pubKvp in PublicationSortHelper.SortByPriority(publications))
        {
            var pubCode = pubKvp.Key;
            var pub = pubKvp.Value;

            // Check if this publication has LanguageId = null (doesn't need a language)
            // This is data-driven, not hard-coded
            if (pub.LanguageId == null)
            {
                Log.Debug("ChooseAsync: Selected publication={PublicationCode} (has LanguageId=null, doesn't need language)",
                    pubCode);
                return (pubCode, pub, true);
            }

            // Publication has LanguageId - check if already harvested, then harvest if needed
            if (languageContentService != null && !language.Code.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    progress?.UpdateProgress(0.3);

                    // Check if publication with first section and tracks is already harvested
                    var isAlreadyHarvested = await sectionTrackResolver.CheckIfPublicationWithFirstSectionHarvestedAsync(
                        pubCode,
                        language.Code);

                    if (!isAlreadyHarvested)
                    {
                        // Harvest the publication (EnsurePublicationExistsAsync checks if it exists first)
                        await languageContentService.EnsurePublicationExistsAsync(pubCode, language.Code, default, progress);
                    }
                    else
                    {
                        Log.Debug("ChooseAsync: Publication={PublicationCode} for language={LanguageCode} already harvested with first section and tracks",
                            pubCode,
                            language.Code);
                        progress?.UpdateProgress(0.5);
                        await Task.Delay(100);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "ChooseAsync: Failed to harvest publication={PublicationCode} for language={LanguageCode}, trying next",
                        pubCode,
                        language.Code);
                    continue;
                }
            }

            // Check if this publication can be queried with a language (has LanguageId)
            // Re-query from database to get the actual publication with correct localized name (not placeholder)
            BiblePublication? queriedPub = null;
            queriedPub = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(language.Code, pubCode);
            var canQueryWithLanguage = queriedPub != null;

            if (canQueryWithLanguage && queriedPub != null)
            {
                Log.Debug("ChooseAsync: Selected publication={PublicationCode} (can be queried with language={LanguageCode})",
                    pubCode,
                    language.Code);
                return (pubCode, queriedPub, false);
            }

            Log.Debug("ChooseAsync: Skipping publication={PublicationCode} (cannot be queried with language={LanguageCode})",
                pubCode,
                language.Code);
        }

        return (null, null, false);
    }
}

