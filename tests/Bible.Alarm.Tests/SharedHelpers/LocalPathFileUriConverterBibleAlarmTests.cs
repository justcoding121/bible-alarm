#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class LocalPathFileUriConverterBibleAlarmTests
{
    [Fact]
    public void TryCreateUriString_returns_file_uri_for_relative_path()
    {
        Assert.True(LocalPathFileUriConverter.TryCreateUriString(
            Path.Combine("subdir", "art.png"),
            out var uri));

        Assert.NotNull(uri);
        Assert.StartsWith("file:///", uri, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("art.png", uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreateUriString_returns_false_for_invalid_path_characters()
    {
        Assert.Contains(Path.GetInvalidPathChars(), c =>
            !LocalPathFileUriConverter.TryCreateUriString($"x{c}y.png", out _));
    }

    [Fact]
    public void TryCreateUriString_throws_for_null_path()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LocalPathFileUriConverter.TryCreateUriString(null!, out _));
    }
}
