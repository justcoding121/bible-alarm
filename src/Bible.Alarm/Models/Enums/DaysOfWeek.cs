using System;
using System.Collections.Generic;

namespace Bible.Alarm.Models
{
    public enum DaysOfWeek
    {
        Sunday = 1,
        Monday = 2,
        Tuesday = 4,
        Wednesday = 8,
        Thursday = 16,
        Friday = 32,
        Saturday = 64,
        All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday
    }

    public static class DaysOfWeekExtensions
    {
        public static List<int> ToList(this DaysOfWeek daysOfWeek)
        {
            var result = new List<int>();

            int day = 1;
            foreach (var item in Enum.GetValues(typeof(DaysOfWeek)))
            {
                if ((DaysOfWeek)item == DaysOfWeek.All)
                {
                    continue;
                }

                if ((daysOfWeek & (DaysOfWeek)item) == (DaysOfWeek)item)
                {
                    result.Add(day);
                }
                day++;
            }

            return result;
        }
    }
}
