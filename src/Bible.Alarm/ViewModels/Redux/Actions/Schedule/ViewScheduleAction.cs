using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.ViewModels.Redux.Actions.Schedule;

public class ViewScheduleAction : IAction
{
    public AlarmSchedule SelectedSchedule { get; set; }
}