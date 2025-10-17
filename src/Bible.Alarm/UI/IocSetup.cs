using Bible.Alarm.UI.Views;
using Bible.Alarm.UI.Views.Bible;
using Bible.Alarm.UI.Views.General;
using Bible.Alarm.UI.Views.Music;

namespace Bible.Alarm.UI
{
    public static class IocSetup
    {
        public static void Initialize(IContainer container, bool isService)
        {
            if (!isService)
            {
                container.Register(x => new Schedule());

                container.Register(x => new MusicSelection());
                container.Register(x => new SongBookSelection());
                container.Register(x => new TrackSelection(container));

                container.Register(x => new BibleSelection());
                container.Register(x => new BookSelection(container));
                container.Register(x => new ChapterSelection(container));

                container.Register(x => new LanguageModal());

                container.Register(x => new AlarmModal());
                container.Register(x => new BatteryOptimizationExclusionModal());
                container.Register(x => new NumberOfChaptersModal());
                container.Register(x => new MediaProgressModal());
            }
        }
    }
}
