namespace Bible.Alarm.Models
{
    public class PlayItem(NotificationDetail detail, string url)
    {
        public NotificationDetail PlayDetail { get; set; } = detail;

        public string Url { get; set; } = url;

        public override string ToString()
        {
            return PlayDetail.LanguageCode + " " + PlayDetail.PublicationCode + " "
                + (PlayDetail.IsAlarmMusic ? PlayDetail.TrackNumber.ToString()
                : PlayDetail.BookNumber.ToString() + " " + PlayDetail.ChapterNumber.ToString());
        }
    }
}
