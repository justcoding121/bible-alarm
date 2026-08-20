#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

/// <summary>
/// Stores the CDN URL for a track. Populated from the publication or section fetch response.
/// One-to-one with BiblePublicationTrack.
/// </summary>
[Table("TrackUrls")]
public sealed class TrackUrl
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The CDN URL for the track (from GETPUBMEDIALINKS or mediator response files[lang].MP3/MP4[].file.url).
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional FK to the track this URL belongs to (1:1).
    /// </summary>
    [ForeignKey(nameof(BiblePublicationTrack))]
    public int? BiblePublicationTrackId { get; set; }

    public BiblePublicationTrack? BiblePublicationTrack { get; set; }
}
