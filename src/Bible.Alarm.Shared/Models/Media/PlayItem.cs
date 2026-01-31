namespace Bible.Alarm.Shared.Models.Media;

public class PlayItem(TrackMetadata metadata, string url)
{
    public TrackMetadata Metadata { get; set; } = metadata;

    public string Url { get; set; } = url;

    public override string ToString()
    {
        return Metadata.LanguageCode + " " + Metadata.PublicationCode + " "
            + (Metadata.IsAlarmMusic ? Metadata.TrackNumber.ToString()
            : (Metadata.SectionCode ?? string.Empty) + " " + Metadata.TrackNumber);
    }
}
