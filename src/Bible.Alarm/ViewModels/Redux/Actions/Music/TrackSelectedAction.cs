using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.ViewModels.Redux.Actions.Music;

public class TrackSelectedAction : IAction
{
    public AlarmMusic CurrentMusic { get; set; }
}