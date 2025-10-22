using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.ViewModels.Redux.Actions.Music;

public class SongBookSelectionAction : IAction
{
    public AlarmMusic TentativeMusic { get; set; }
}