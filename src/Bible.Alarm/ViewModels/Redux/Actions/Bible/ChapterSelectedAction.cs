using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.ViewModels.Redux.Actions.Bible;

public class ChapterSelectedAction : IAction
{
    public BibleReadingSchedule CurrentBibleReadingSchedule { get; set; }
}