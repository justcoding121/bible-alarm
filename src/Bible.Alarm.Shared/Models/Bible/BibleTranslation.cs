using System.Collections.Generic;

namespace Bible.Alarm.Shared.Models.Bible;

public class BibleTranslation : Publication
{
    public Language Language { get; set; }
    public List<BibleBook> Books { get; set; } = new();
}
