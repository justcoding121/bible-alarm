using System;

namespace Bible.Alarm.Audio.Links.Harvester.Models.Bible
{
    public class BibleBook : IComparable
    {
        public string Name { get; set; }
        public int Number { get; set; }

        public int CompareTo(object obj)
        {
            return Number.CompareTo((obj as BibleBook).Number);
        }
    }
}
