using Bible.Alarm.Shared.Models.Bible;
using Bible.Alarm.Shared.Models;

namespace Bible.Alarm.Models;

public class BibleTranslation : TranslatedPublication
{
    public int Id { get; set; }
    public List<BibleBook> Books { get; set; } = [];
}