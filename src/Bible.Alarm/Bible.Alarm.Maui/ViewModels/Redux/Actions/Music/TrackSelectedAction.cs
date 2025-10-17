using Bible.Alarm.Models;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions.Music
{
    public class TrackSelectedAction : IAction
    {
        public AlarmMusic CurrentMusic { get; set; }
    }
}
