#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class LocalPathFileUriConverterTests
{
    [Fact]
    public void TryCreateUriString_returns_file_uri_for_existing_style_relative_path()
    {
        Assert.True(LocalPathFileUriConverter.TryCreateUriString(
            Path.Combine("subdir", "art.png"),
            out var uri));

        Assert.NotNull(uri);
        Assert.StartsWith("file:///", uri, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("art.png", uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreateUriString_returns_false_for_some_invalid_path_characters()
    {
        Assert.Contains(Path.GetInvalidPathChars(), c =>
            !LocalPathFileUriConverter.TryCreateUriString($"x{c}y.png", out _));
    }
}
