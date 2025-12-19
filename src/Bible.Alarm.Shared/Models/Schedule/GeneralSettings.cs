#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Models;

[Table("GeneralSettings")]
[Index(nameof(Key), IsUnique = true)]
public class GeneralSettings
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }
}