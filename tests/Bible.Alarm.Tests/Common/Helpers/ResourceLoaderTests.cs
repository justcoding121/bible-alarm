#nullable enable

using System.Reflection;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media;

namespace Bible.Alarm.Tests;

public sealed class ResourceLoaderTests
{
    private static Assembly BibleAlarmAssembly => typeof(FallbackAlarmSoundService).Assembly;

    [Fact]
    public void GetEmbeddedResourceStream_returns_stream_for_known_embedded_file()
    {
        using var stream = ResourceLoader.GetEmbeddedResourceStream(BibleAlarmAssembly, "schedule.db");

        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public void GetEmbeddedResourceStream_throws_when_no_matching_resource()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ResourceLoader.GetEmbeddedResourceStream(BibleAlarmAssembly, "__no_such_resource_xyz__.bin"));
    }

    [Fact]
    public void GetFileInfo_matches_assembly_location_or_throws_when_unavailable()
    {
#pragma warning disable IL3000, IL3002 // Single-file / RequiresAssemblyFiles: this test asserts real on-disk layout vs. Location being empty.
        var asm = BibleAlarmAssembly;

        if (string.IsNullOrEmpty(asm.Location))
        {
            Assert.Throws<InvalidOperationException>(() => ResourceLoader.GetFileInfo(asm));
        }
        else
        {
            var fi = ResourceLoader.GetFileInfo(asm);

            var expectedFull = new FileInfo(asm.Location).FullName;
            var actualFull = fi.FullName;
            Assert.Equal(expectedFull, actualFull, StringComparer.OrdinalIgnoreCase);
        }
#pragma warning restore IL3000, IL3002
    }
}
