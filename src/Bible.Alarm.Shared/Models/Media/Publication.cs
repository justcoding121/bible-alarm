using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Models.Media;

public class Publication : IComparable
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("PublicationCode")]
    public string PublicationCode { get; set; } = string.Empty;

    public int CompareTo(object obj) => Name.CompareTo((obj as Publication).Name);
}

public class TranslatedPublication : Publication
{
    [Required]
    [ForeignKey(nameof(Language))]
    public int LanguageId { get; set; }

    [Required]
    public virtual Language Language { get; set; } = null!;
}
