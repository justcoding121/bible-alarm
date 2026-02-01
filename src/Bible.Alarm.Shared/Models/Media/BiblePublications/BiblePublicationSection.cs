using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Helpers;
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

    public int CompareTo(object obj)
    {
        if (obj is not BiblePublicationSection other)
        {
            return 1;
        }

        // IMPORTANT:
        // Section codes are stored/treated as strings throughout the app.
        // The only place we interpret them numerically is for ordering (natural sort).
        return SectionCodeHelper.SectionCodeComparer.Compare(SectionCode, other.SectionCode);
    }
}
