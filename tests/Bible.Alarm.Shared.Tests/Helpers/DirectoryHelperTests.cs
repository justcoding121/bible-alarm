using System.IO;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class DirectoryHelperTests
{
    [Fact]
    public void Ensure_CreatesFolder_WhenMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bible-alarm-tests-{Guid.NewGuid():n}");
        var dir = Path.Combine(root, "nested");
        try
        {
            Assert.False(Directory.Exists(dir));
            DirectoryHelper.Ensure(dir);
            Assert.True(Directory.Exists(dir));
            DirectoryHelper.Ensure(dir);
            Assert.True(Directory.Exists(dir));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Ensure_CreatesNestedPath_When_IntermediateParentsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bible-alarm-tests-{Guid.NewGuid():n}");
        var deep = Path.Combine(root, "a", "b", "c");
        try
        {
            Assert.False(Directory.Exists(deep));
            DirectoryHelper.Ensure(deep);
            Assert.True(Directory.Exists(deep));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void IndexDirectory_ReturnsStablePath_OnRepeated_Access()
    {
        var first = DirectoryHelper.IndexDirectory;
        var second = DirectoryHelper.IndexDirectory;
        Assert.Equal(first, second);
        Assert.False(string.IsNullOrEmpty(first));
    }

    [Fact]
    public void IndexDirectory_Resolves_ToSrcTools_IndexRoot()
    {
        var path = Path.GetFullPath(DirectoryHelper.IndexDirectory);
        var suffix = $"{Path.DirectorySeparatorChar}_tools{Path.DirectorySeparatorChar}_index";

        Assert.Contains(suffix, path, StringComparison.Ordinal);
    }
}
