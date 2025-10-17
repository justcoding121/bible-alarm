using Bible.Alarm.Models;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions
{
    public class SongBookSelectionAction : IAction
    {
        public AlarmMusic TentativeMusic { get; set; }
    }
}
