using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Bible.Alarm.Contracts.Media
{
    public interface IAndroidAlarmHandler 
    {
        Task Handle(long scheduleId, bool isImmediate);
    }
}
