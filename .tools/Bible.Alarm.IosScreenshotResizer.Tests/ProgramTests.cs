using ResizerProgram = Bible.Alarm.IosScreenshotResizer.Program;

namespace Bible.Alarm.IosScreenshotResizer.Tests;

public class ProgramTests
{
    [Fact]
    public void Main_WithMissingFolder_Returns1()
    {
        var code = ResizerProgram.Main([Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_missing")]);

        Assert.Equal(1, code);
    }

    [Fact]
    public void Main_WithEmptyFolder_Returns0()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        try
        {
            var code = ResizerProgram.Main([dir]);

            Assert.Equal(0, code);
        }
        finally
        {
            Directory.Delete(dir);
        }
    }
}
