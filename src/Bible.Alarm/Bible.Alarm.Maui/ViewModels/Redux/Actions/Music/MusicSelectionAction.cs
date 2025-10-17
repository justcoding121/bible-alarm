using Bible.Alarm.Models;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions
{
    public class MusicSelectionAction : IAction
    {
        public AlarmMusic CurrentMusic { get; set; }
    }
}
