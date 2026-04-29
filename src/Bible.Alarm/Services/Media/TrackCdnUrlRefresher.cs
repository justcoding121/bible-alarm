#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

/// <inheritdoc />
public sealed class TrackCdnUrlRefresher(
    ILanguageContentService languageContentService,
    IUrlConstructionService urlConstructionService,
    IMelodyDiscTracksApiRefresher melodyDiscTracksApiRefresher,
    ILogger logger) : ITrackCdnUrlRefresher
{
    /// <inheritdoc />
    public async Task<string?> TryRefreshTrackCdnUrlFromApiAsync(TrackMetadata metadata, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(metadata.TrackCode))
        {
            logger.Warning(AppConstants.Logging.TrackCdnUrlRefresherDiagnosticsLog.MissingTrackCode);
            return null;
        }

        var isMelodyDisc =
            string.IsNullOrWhiteSpace(metadata.LanguageCode) &&
            !string.IsNullOrWhiteSpace(metadata.DownloadCode);

        if (isMelodyDisc)
        {
            var melodyOk = await melodyDiscTracksApiRefresher.ReplaceDiscSectionTracksFromApiAsync(
                metadata.PublicationCode,
                metadata.DownloadCode!,
                cancellationToken);

            urlConstructionService.ClearLookUpPathCache();

            if (!melodyOk)
            {
                logger.Warning(
                    AppConstants.Logging.TrackCdnUrlRefresherDiagnosticsLog.MelodyDiscRefreshFailedPubDisc,
                    metadata.PublicationCode,
                    metadata.DownloadCode);
                return null;
            }

            var melodyUrls = await urlConstructionService.ConstructTrackUrlsAsync(
                metadata.PublicationCode,
                string.Empty,
                metadata.DownloadCode,
                metadata.TrackCode);

            return melodyUrls is { Count: > 0 } list ? list[0] : null;
        }

        var lang = (metadata.LanguageCode ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(lang))
        {
            logger.Warning(AppConstants.Logging.TrackCdnUrlRefresherDiagnosticsLog.MissingLanguageCodeForNonMelodyTrack);
            return null;
        }

        bool ok;
        if (!string.IsNullOrWhiteSpace(metadata.SectionCode))
        {
            ok = await languageContentService.FetchSectionTracksAsync(
                metadata.PublicationCode,
                metadata.SectionCode.Trim(),
                lang,
                replaceExistingTracksFromApi: true,
                cancellationToken: cancellationToken);
        }
        else
        {
            ok = await languageContentService.FetchPublicationTracksAsync(
                metadata.PublicationCode,
                lang,
                cancellationToken);
        }

        urlConstructionService.ClearLookUpPathCache();

        if (!ok)
        {
            logger.Warning(
                AppConstants.Logging.TrackCdnUrlRefresherDiagnosticsLog.ApiRefreshFailedPubLangSection,
                metadata.PublicationCode,
                lang,
                metadata.SectionCode ?? "(flat)");
            return null;
        }

        var urls = await urlConstructionService.ConstructTrackUrlsAsync(
            metadata.PublicationCode,
            lang,
            metadata.SectionCode,
            metadata.TrackCode);

        return urls.Count > 0 ? urls[0] : null;
    }
}
