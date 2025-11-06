using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Stores.Actions.Music;

public class MusicSelectionAction : IAction
{
    public AlarmMusic CurrentMusic { get; set; }
}