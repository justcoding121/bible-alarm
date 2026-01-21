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

    /// <summary>
    /// Section code (e.g., "1" for book 1, "gen" for Genesis, section code for dramas).
    /// Matches SectionLanguage.SectionCode for consistency.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SectionCode { get; set; } = string.Empty;

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

    public int CompareTo(object obj)
    {
        if (obj is not BiblePublicationSection other)
        {
            return 1;
        }
        
        // Natural sort: if SectionCode is numeric, sort as int; otherwise sort as string
        var thisIsNumeric = int.TryParse(SectionCode, out var thisNum);
        var otherIsNumeric = int.TryParse(other.SectionCode, out var otherNum);
        
        if (thisIsNumeric && otherIsNumeric)
        {
            // Both are numeric - compare as integers
            return thisNum.CompareTo(otherNum);
        }
        
        if (thisIsNumeric && !otherIsNumeric)
        {
            // This is numeric, other is not - numeric comes first
            return -1;
        }
        
        if (!thisIsNumeric && otherIsNumeric)
        {
            // This is not numeric, other is - numeric comes first
            return 1;
        }
        
        // Both are non-numeric - compare as strings
        return string.Compare(SectionCode, other.SectionCode, StringComparison.OrdinalIgnoreCase);
    }
}
