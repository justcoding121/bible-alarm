#nullable enable

using Bible.Alarm.Shared.Constants;
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
            using var pubEnumerator = publications.GetEnumerator();
            _ = pubEnumerator.MoveNext();
            var firstPub = pubEnumerator.Current;
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
                Log.Debug(AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChooseSelectedPublicationNoLanguageNeeded,
                    pubCode);
                return (pubCode, pub, true);
            }

            // Publication has LanguageId - check if already cataloged, then catalog if needed
            if (languageContentService != null && !language.Code.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    progress?.UpdateProgress(0.3);

                    // Check if publication with first section and tracks is already cataloged
                    var isAlreadyCataloged = await sectionTrackResolver.CheckIfPublicationWithFirstSectionCatalogedAsync(
                        pubCode,
                        language.Code);

                    if (!isAlreadyCataloged)
                    {
                        // Catalog the publication (EnsurePublicationExistsAsync checks if it exists first)
                        await languageContentService.EnsurePublicationExistsAsync(pubCode, language.Code, progress);
                    }
                    else
                    {
                        Log.Debug(AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChoosePublicationAlreadyCatalogedWithSectionAndTracks,
                            pubCode,
                            language.Code);
                        progress?.UpdateProgress(0.5);
                        await Task.Delay(100);
                    }
                }
                catch (Exception ex)
                {
                    if (Bible.Alarm.Shared.Helpers.NetworkExceptionHelper.IsNetworkFailure(ex))
                    {
                        throw;
                    }

                    Log.Debug(ex, AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChooseFailedToCatalogPublicationTryingNext,
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
                Log.Debug(AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChooseSelectedPublicationQueryableWithLanguage,
                    pubCode,
                    language.Code);
                return (pubCode, queriedPub, false);
            }

            Log.Debug(AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChooseSkippingPublicationNotQueryableWithLanguage,
                pubCode,
                language.Code);
        }

        return (null, null, false);
    }
}

