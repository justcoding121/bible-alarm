#nullable enable

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Language/section identity and URL/title parsing toggles for <see cref="MediatorTrackParser"/>.
/// </summary>
internal readonly record struct MediatorTrackParseContext(
    string NormalizedLanguageCode,
    string SectionCode,
    bool IsVideo = false,
    int? TrackNumber = null,
    bool AllowAudioDescriptionTitles = false,
    bool OmitTrackFromUrlParams = false,
    bool UseDocidParam = false);
