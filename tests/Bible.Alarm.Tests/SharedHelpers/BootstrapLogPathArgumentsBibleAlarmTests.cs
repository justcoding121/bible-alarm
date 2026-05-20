#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class BootstrapLogPathArgumentsBibleAlarmTests
{
    [Fact]
    public void Validate_throws_when_root_directory_is_null_or_whitespace()
    {
        Assert.Throws<ArgumentException>(() => BootstrapLogPathArguments.Validate("  ", "logs", "bootstrap.txt"));
    }

    [Fact]
    public void Validate_throws_when_logs_directory_name_is_null_or_whitespace()
    {
        Assert.Throws<ArgumentException>(() => BootstrapLogPathArguments.Validate(@"C:\root", "\t", "bootstrap.txt"));
    }

    [Fact]
    public void Validate_throws_when_file_name_is_null_or_whitespace()
    {
        Assert.Throws<ArgumentException>(() => BootstrapLogPathArguments.Validate(@"C:\root", "logs", ""));
    }
}
