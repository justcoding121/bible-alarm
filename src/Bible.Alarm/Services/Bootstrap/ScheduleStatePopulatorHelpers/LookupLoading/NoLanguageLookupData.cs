#nullable enable

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Internal result type for no-language lookup loading in LookupDataLoader.
/// </summary>
internal sealed record NoLanguageLookupData(
    Dictionary<string, NoLanguagePublicationMeta> Publications,
    Dictionary<(string PublicationCode, string SectionCode), string> Sections,
    Dictionary<(string PublicationCode, string? SectionCode, string TrackCode), string> TrackTitles)
{
    public static readonly NoLanguageLookupData Empty =
        new(new Dictionary<string, NoLanguagePublicationMeta>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<(string PublicationCode, string SectionCode), string>(),
            new Dictionary<(string PublicationCode, string? SectionCode, string TrackCode), string>());
}
