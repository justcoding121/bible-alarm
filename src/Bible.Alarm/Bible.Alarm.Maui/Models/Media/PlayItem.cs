namespace Bible.Alarm.Models
{
    public class PlayItem
    {
        public NotificationDetail PlayDetail { get; set; }

        public string Url { get; set; }

        public PlayItem(NotificationDetail detail, string url)
        {
            PlayDetail = detail;
            Url = url;
        }

        public override string ToString()
        {
            return PlayDetail.LanguageCode + " " + PlayDetail.PublicationCode + " "
                + (PlayDetail.IsAlarmMusic ? PlayDetail.TrackNumber.ToString()
                : PlayDetail.BookNumber.ToString() + " " + PlayDetail.ChapterNumber.ToString());
        }
    }
}
