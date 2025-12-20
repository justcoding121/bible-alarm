using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.Bible;

[Table("BibleTranslations")]
[Index(nameof(Code), nameof(LanguageId), IsUnique = true)]
public sealed class BibleTranslation : TranslatedPublication
{
    [Key]
    public int Id { get; set; }

    [Required]
    public List<BibleBook> Books { get; set; } = [];
}
