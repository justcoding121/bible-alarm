#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class Utf8LookupPathSafeFilenameTokenTests
{
    [Fact]
    public void FromLookupPath_strips_padding_equals_and_maps_base64_specials()
    {
        var bytes = new byte[] { 251, 255, 254 }; // yields Base64 ending with padding "=" for typical alphabet
        var lookupPath = System.Text.Encoding.UTF8.GetString(bytes);

        var token = Utf8LookupPathSafeFilenameToken.FromLookupPath(lookupPath);

        Assert.DoesNotContain('=', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('+', token);
    }

    [Fact]
    public void FromLookupPath_throws_when_lookup_path_null()
    {
        Assert.Throws<ArgumentNullException>(() => Utf8LookupPathSafeFilenameToken.FromLookupPath(null!));
    }
}
