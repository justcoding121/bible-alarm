using System;
using Newtonsoft.Json;

namespace Bible.Alarm.Shared.Models.Bible
{
    public class BibleChapter : IComparable
    {
        public int Number { get; set; }
        public string Url { get; set; }

        [JsonIgnore]
        public string Title => $"Chapter {Number}";

        public int CompareTo(object obj)
        {
            return Number.CompareTo((obj as BibleChapter).Number);
        }
    }
}
