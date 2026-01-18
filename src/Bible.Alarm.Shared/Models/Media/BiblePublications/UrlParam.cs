#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

/// <summary>
/// Stores URL parameters as key-value pairs for constructing file URLs.
/// Each record represents a single parameter. Multiple records can belong to the same entity.
/// </summary>
[Table("UrlParams")]
public sealed class UrlParam
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Optional foreign key to BiblePublication.
    /// Only one of BiblePublicationId, BiblePublicationSectionId, or BiblePublicationTrackId should be set.
    /// </summary>
    [ForeignKey(nameof(BiblePublication))]
    public int? BiblePublicationId { get; set; }

    /// <summary>
    /// Optional navigation property to BiblePublication.
    /// </summary>
    public BiblePublication? BiblePublication { get; set; }

    /// <summary>
    /// Optional foreign key to BiblePublicationSection.
    /// Only one of BiblePublicationId, BiblePublicationSectionId, or BiblePublicationTrackId should be set.
    /// </summary>
    [ForeignKey(nameof(BiblePublicationSection))]
    public int? BiblePublicationSectionId { get; set; }

    /// <summary>
    /// Optional navigation property to BiblePublicationSection.
    /// </summary>
    public BiblePublicationSection? BiblePublicationSection { get; set; }

    /// <summary>
    /// Optional foreign key to BiblePublicationTrack.
    /// Only one of BiblePublicationId, BiblePublicationSectionId, or BiblePublicationTrackId should be set.
    /// </summary>
    [ForeignKey(nameof(BiblePublicationTrack))]
    public int? BiblePublicationTrackId { get; set; }

    /// <summary>
    /// Optional navigation property to BiblePublicationTrack.
    /// </summary>
    public BiblePublicationTrack? BiblePublicationTrack { get; set; }

    /// <summary>
    /// Optional foreign key to BaseUrl.
    /// </summary>
    [ForeignKey(nameof(BaseUrl))]
    public int? BaseUrlId { get; set; }

    /// <summary>
    /// Optional navigation property to BaseUrl.
    /// </summary>
    public Media.BaseUrl? BaseUrl { get; set; }

    /// <summary>
    /// Parameter key (e.g., "pub", "booknum", "track", "fileformat").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Parameter value (e.g., "nwt", "1", "mp3").
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this parameter should be included as a query parameter in the URL.
    /// If false, the parameter may be used for other purposes (e.g., path construction).
    /// </summary>
    [Required]
    public bool IsQueryParam { get; set; }
}
