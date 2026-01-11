using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.Music;

[Table("MelodyMusics")]
[Index(nameof(Code), IsUnique = true)]
public sealed class MelodyMusic : Publication
{
    [Key]
    public int Id { get; set; }

    [Required]
    public List<MusicTrack> Tracks { get; set; } = [];
}
