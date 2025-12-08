using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.Bible
{
    [Table("BibleBook")]
    [Index(nameof(BibleTranslationId), nameof(Number), IsUnique = true)]
    public class BibleBook : IComparable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(1, 66)]
        public int Number { get; set; }

        [Required]
        [ForeignKey(nameof(BibleTranslation))]
        public int BibleTranslationId { get; set; }

        [Required]
        public virtual BibleTranslation BibleTranslation { get; set; } = null!;

        [Required]
        public List<BibleChapter> Chapters { get; set; } = [];

        public int CompareTo(object obj)
        {
            if (obj is not BibleBook other) return 1;
            return Number.CompareTo(other.Number);
        }
    }
}
