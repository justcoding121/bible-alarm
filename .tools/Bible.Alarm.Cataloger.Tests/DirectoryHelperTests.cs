using Bible.Alarm.Cataloger.Utility;

namespace Bible.Alarm.Cataloger.Tests;

public class DirectoryHelperTests
{
    [Fact]
    public void Ensure_CreatesDirectory_WhenMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            DirectoryHelper.Ensure(path);

            Assert.True(Directory.Exists(path));
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path);
            }
        }
    }
}
