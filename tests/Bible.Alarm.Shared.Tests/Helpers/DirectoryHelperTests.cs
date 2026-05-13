#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class DirectoryHelperTests
{
    [Fact]
    public void Ensure_creates_directory_when_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ba-dir-helper-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(dir));

        try
        {
            DirectoryHelper.Ensure(dir);

            Assert.True(Directory.Exists(dir));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
