using System.Collections.Generic;
using System.Linq;

namespace Bible.Alarm.Common.Helpers
{
    public class BgSourceHelper
    {
        public static Dictionary<string, string> PublicationCodeToNameMappings = new[]
        {
            new KeyValuePair<string, string>("kjv","King James Version (1987)"),
            new KeyValuePair<string, string>("nivuk","New International Version—Anglicised (1984)")

        }.ToDictionary(x => x.Key, x => x.Value);

        public static Dictionary<int, string> BookNumberToNamesMap = getBookNames();

        private static Dictionary<int, string> getBookNames()
        {
            return new string[] { "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy", "Joshua", "Judges", "Ruth", "1 Samuel", "2 Samuel", "1 Kings", "2 Kings", "1 Chronicles", "2 Chronicles", "Ezra", "Nehemiah", "Esther", "Job", "Psalms", "Proverbs", "Ecclesiastes", "Song of Solomon", "Isaiah", "Jeremiah", "Lamentations", "Ezekiel", "Daniel", "Hosea", "Joel", "Amos", "Obadiah", "Jonah", "Micah", "Nahum", "Habakkuk", "Zephaniah", "Haggai", "Zechariah", "Malachi", "Matthew", "Mark", "Luke", "John", "Acts (of the Apostles)", "Romans", "1 Corinthians", "2 Corinthians", "Galatians", "Ephesians", "Philippians", "Colossians", "1 Thessalonians", "2 Thessalonians", "1 Timothy", "2 Timothy", "Titus", "Philemon", "Hebrews", "James", "1 Peter", "2 Peter", "1 John", "2 John", "3 John", "Jude", "Revelation" }
                       .Select((s, i) => new { i = i + 1, s })
                       .ToDictionary(x => x.i, x => x.s);
        }
    }

}
