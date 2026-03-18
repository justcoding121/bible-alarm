namespace Bible.Alarm.Shared.Models.Media;

public class PlayItem(TrackMetadata metadata, string url)
{
    public TrackMetadata Metadata { get; set; } = metadata;

    public string Url { get; set; } = url;

    /// <summary>
    /// After CDN 404/410, true once we have attempted catalog refresh for this item (whether or not replay ran).
    /// </summary>
    public bool CdnStaleUrlRecoveryConsumed { get; set; }

    /// <summary>
    /// True after we re-fetched catalog and auto-started playback once on a new URL.
    /// </summary>
    public bool CdnStaleUrlRefetchReplayIssued { get; set; }

    /// <summary>
    /// One silent re-play after MediaFailed during stream open/buffer (before playback starts).
    /// </summary>
    public bool StreamingOpenPhaseMediaFailedRetryDone { get; set; }

    public override string ToString()
    {
        return Metadata.LanguageCode + " " + Metadata.PublicationCode + " "
            + (Metadata.IsAlarmMusic ? Metadata.TrackCode.ToString()
            : (Metadata.SectionCode ?? string.Empty) + " " + Metadata.TrackCode);
    }
}
