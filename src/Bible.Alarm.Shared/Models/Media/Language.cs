using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media;

[Table("Languages")]
[Index(nameof(Code), IsUnique = true)]
public sealed class Language : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Text direction: "ltr" (left-to-right) or "rtl" (right-to-left)
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string Direction { get; set; } = "ltr";

    public int CompareTo(object obj) => Name.CompareTo((obj as Language).Name);
}
