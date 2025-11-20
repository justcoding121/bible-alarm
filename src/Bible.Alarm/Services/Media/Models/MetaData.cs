#nullable enable
namespace Bible.Alarm.Services.Media.Models;

public class MetaData
{
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? ArtworkUrl { get; set; }
    public byte[]? ArtworkBytes { get; set; }
}

