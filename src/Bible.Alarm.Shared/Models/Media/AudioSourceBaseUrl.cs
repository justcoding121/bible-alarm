using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media;

/// <summary>
/// Stores unique base URLs for audio sources to avoid redundant storage.
/// Example: "https://cfp2.jw-cdn.org" which is shared across many audio sources.
/// </summary>
[Table("AudioSourceBaseUrls")]
public class AudioSourceBaseUrl
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The base URL (scheme + host), e.g., "https://cfp2.jw-cdn.org"
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string BaseUrl { get; set; } = string.Empty;
}
