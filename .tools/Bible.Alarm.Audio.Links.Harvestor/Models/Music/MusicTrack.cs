using System;

namespace AudioLinkHarvester.Models.Music
{
    public class MusicTrack : IComparable
    {
        public int Number { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }

        public string LookUpPath { get; set; }

        public int CompareTo(object obj)
        {
            return Number.CompareTo((obj as MusicTrack).Number);
        }
    }
}
