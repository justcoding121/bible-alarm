#nullable enable

namespace Bible.Alarm.Shared.Constants;

/// <summary>
/// URI scheme prefixes for CDN streaming and local file artwork paths.
/// </summary>
public static class MediaUriSchemeConstants
{
    public const string FilePrefix = "file://";

    /// <summary><c>file:///</c> variant used on some Unix-style absolute URIs.</summary>
    public const string FileUriTripleSlashPrefix = FilePrefix + "/";

    public const string HttpsPrefix = "https://";
    public const string HttpPrefix = "http://";
}

/// <summary>
/// MIME strings for TagLib.File.Create format selection when probing parsers.
/// </summary>
public static class TagLibMimeConstants
{
    public const string AudioMpeg = "audio/mpeg";
    public const string VideoMp4 = "video/mp4";
}
