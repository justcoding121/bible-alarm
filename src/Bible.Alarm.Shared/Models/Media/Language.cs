#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media;

[Table("Languages")]
[Index(nameof(LanguageCode), IsUnique = true)]
public sealed class Language : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("LanguageCode")]
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Text direction: "ltr" (left-to-right) or "rtl" (right-to-left)
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string Direction { get; set; } = "ltr";

    /// <summary>
    /// Localized names per display language code (e.g. "E" for English).
    /// </summary>
    public List<LanguageNameByLanguage> NamesByDisplayLanguage { get; set; } = [];

    public int CompareTo(object? obj)
    {
        if (obj is not Language other)
        {
            return 1;
        }

        return string.Compare(LanguageCode, other.LanguageCode, StringComparison.Ordinal);
    }
}
