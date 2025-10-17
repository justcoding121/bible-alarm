using Bible.Alarm.Models;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions.Music
{
    public class TrackSelectionAction : IAction
    {
        public AlarmMusic TentativeMusic { get; set; }
    }
}
