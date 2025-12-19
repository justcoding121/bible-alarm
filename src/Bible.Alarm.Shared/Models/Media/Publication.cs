using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media;

public class Publication : IComparable
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(DisplayLanguage))]
    public int DisplayLanguageId { get; set; }

    [Required]
    public virtual Language DisplayLanguage { get; set; } = null!;

    public int CompareTo(object obj)
    {
        return Name.CompareTo((obj as Publication).Name);
    }
}

public class TranslatedPublication : Publication
{
    [Required]
    [ForeignKey(nameof(Language))]
    public int LanguageId { get; set; }

    [Required]
    public virtual Language Language { get; set; } = null!;
}
