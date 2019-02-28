﻿using JW.Alarm.Common.DataStructures;
using JW.Alarm.Models;

namespace JW.Alarm.ViewModels.Redux
{
    public class ApplicationState
    {
        public ObservableHashSet<ScheduleListItem> Schedules { get; set; }

        public ScheduleListItem CurrentScheduleListItem { get; set; }

        public AlarmMusic CurrentMusic { get; set; }
        public AlarmMusic TentativeMusic { get; set; }
    }
}
