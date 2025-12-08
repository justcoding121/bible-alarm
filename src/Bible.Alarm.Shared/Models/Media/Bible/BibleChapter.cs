using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.Bible
{
    [Table("BibleChapter")]
    [Index(nameof(BibleBookId), nameof(Number), IsUnique = true)]
    public class BibleChapter : IComparable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Range(1, 150)]
        public int Number { get; set; }

        public string Title => $"Chapter {Number}";

        public AudioSource? Source { get; set; }

        [Required]
        [ForeignKey(nameof(Book))]
        public int BibleBookId { get; set; }

        [Required]
        public virtual BibleBook Book { get; set; } = null!;

        public int CompareTo(object obj)
        {
            if (obj is not BibleChapter other) return 1;
            return Number.CompareTo(other.Number);
        }
    }
}
