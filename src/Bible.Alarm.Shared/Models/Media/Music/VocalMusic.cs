using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.Music;

[Table("VocalMusic")]
[Index(nameof(Code), nameof(LanguageId), IsUnique = true)]
public class VocalMusic : TranslatedPublication
{
    [Key]
    public int Id { get; set; }

    [Required]
    public List<MusicTrack> Tracks { get; set; } = [];
}
