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
            return SelectFirstPublicationWhenServiceUnavailable(publications);
        }

        foreach (var pubKvp in PublicationSortHelper.SortByPriority(publications))
        {
            var resolved = await TryResolvePublicationForPriorityEntryAsync(pubKvp, language, progress);
            if (resolved != null)
            {
                return resolved.Value;
            }
        }

        return (null, null, false);
    }

    private static (string? PublicationCode, BiblePublication? Publication, bool PublicationWithoutLanguage)
        SelectFirstPublicationWhenServiceUnavailable(Dictionary<string, BiblePublication> publications)
    {
        using var pubEnumerator = publications.GetEnumerator();
        _ = pubEnumerator.MoveNext();
        var firstPub = pubEnumerator.Current;
        return (firstPub.Key, firstPub.Value, firstPub.Value.LanguageId == null);
    }

    private async Task<(string PublicationCode, BiblePublication Publication, bool PublicationWithoutLanguage)?>
        TryResolvePublicationForPriorityEntryAsync(
            KeyValuePair<string, BiblePublication> pubKvp,
            LanguageListViewItemModel language,
            IFetchProgress? progress)
    {
        var pubCode = pubKvp.Key;
        var pub = pubKvp.Value;

        if (pub.LanguageId == null)
        {
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChooseSelectedPublicationNoLanguageNeeded,
                pubCode);
            return (pubCode, pub, true);
        }

        var catalogSkipped = await TryEnsurePublicationCatalogedAsync(pubCode, language, progress);
        if (!catalogSkipped)
        {
            return null;
        }

        BiblePublication? queriedPub = await biblePublicationService!.GetByLanguageAndCodeWithTracksAsync(language.Code, pubCode);
        if (queriedPub != null)
        {
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChooseSelectedPublicationQueryableWithLanguage,
                pubCode,
                language.Code);
            return (pubCode, queriedPub, false);
        }

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChooseSkippingPublicationNotQueryableWithLanguage,
            pubCode,
            language.Code);
        return null;
    }

    /// <summary>Returns false when outer loop should try next publication (catalog failure).</summary>
    private async Task<bool> TryEnsurePublicationCatalogedAsync(
        string pubCode,
        LanguageListViewItemModel language,
        IFetchProgress? progress)
    {
        if (languageContentService == null || language.Code.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            progress?.UpdateProgress(0.3);

            var isAlreadyCataloged = await sectionTrackResolver.CheckIfPublicationWithFirstSectionCatalogedAsync(
                pubCode,
                language.Code);

            if (!isAlreadyCataloged)
            {
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
            return false;
        }

        return true;
    }
}

