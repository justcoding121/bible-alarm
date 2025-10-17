using System;

namespace Bible.Alarm.Models
{
    public class Publication : IComparable
    {
        public string Name { get; set; }
        public string Code { get; set; }

        public int DisplayLanguageId { get; set; }
        public Language DisplayLanguage { get; set; }

        public int CompareTo(object obj)
        {
            return Name.CompareTo((obj as Publication).Name);
        }
    }

    public class TranslatedPublication : Publication
    {
        public int LanguageId { get; set; }
        public Language Language { get; set; }
    }

}
