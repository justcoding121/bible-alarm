using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media;

[Table("Sources")]
public class Source
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the base URL.
    /// </summary>
    [Required]
    [ForeignKey(nameof(BaseUrlEntity))]
    public int BaseUrlId { get; set; }

    /// <summary>
    /// Navigation property to the base URL entity.
    /// </summary>
    [Required]
    public SourceBaseUrl BaseUrlEntity { get; set; } = null!;

    /// <summary>
    /// The URL path (excluding the base URL), e.g., "/a/64c70d/1/o/osg_MY_098.mp3"
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string UrlPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets the full URL by combining BaseUrl and UrlPath.
    /// This is a computed property, not stored in the database.
    /// </summary>
    [NotMapped]
    public string Url => BaseUrlEntity?.BaseUrl + UrlPath;
}
