using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media
{
    [Table("AudioSource")]
    public class AudioSource
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string LookUpPath { get; set; } = string.Empty;
    }
}
