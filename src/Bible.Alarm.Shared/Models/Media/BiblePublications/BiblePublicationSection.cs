using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

[Table("BiblePublicationSections")]
[Index(nameof(BiblePublicationId), IsUnique = false)]
public sealed class BiblePublicationSection : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int Number { get; set; }

    [Required]
    [ForeignKey(nameof(BiblePublication))]
    public int BiblePublicationId { get; set; }

    [Required]
    public BiblePublication BiblePublication { get; set; } = null!;

    /// <summary>
    /// Navigation property to UrlParams (one-to-many, optional).
    /// Contains URL parameters as key-value pairs.
    /// </summary>
    public List<UrlParam> UrlParams { get; set; } = [];

    [Required]
    public List<BiblePublicationTrack> Tracks { get; set; } = [];

    /// <summary>
    /// Helper property to get the booknum value from UrlParams.
    /// Returns null if not found or cannot be parsed.
    /// </summary>
    public int? BookNum
    {
        get
        {
            var booknumParam = UrlParams.FirstOrDefault(p => p.Key.Equals("booknum", StringComparison.OrdinalIgnoreCase));
            if (booknumParam != null && int.TryParse(booknumParam.Value, out var booknum))
            {
                return booknum;
            }
            return null;
        }
    }

    public int CompareTo(object obj) => obj is not BiblePublicationSection other ? 1 : Number.CompareTo(other.Number);
}
