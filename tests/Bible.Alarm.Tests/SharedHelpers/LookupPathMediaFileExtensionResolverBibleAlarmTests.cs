#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class LookupPathMediaFileExtensionResolverBibleAlarmTests
{
    [Fact]
    public void Resolve_https_absolute_aac_path_returns_aac_extension()
    {
        var result = LookupPathMediaFileExtensionResolver.Resolve("https://cdn.example.org/media/lesson.aac");

        Assert.Equal(AppConstants.Media.MediaAacFileExtension, result);
    }

    [Fact]
    public void Resolve_returns_default_mp3_when_no_format_hint_present()
    {
        Assert.Equal(
            AppConstants.Media.MediaFileExtension,
            LookupPathMediaFileExtensionResolver.Resolve("relative/lookup-without-uri"));
    }

    [Fact]
    public void Resolve_https_absolute_path_without_known_suffix_falls_through_to_default()
    {
        Assert.Equal(
            AppConstants.Media.MediaFileExtension,
            LookupPathMediaFileExtensionResolver.Resolve("https://cdn.example.org/media/unknown.bin"));
    }
}
