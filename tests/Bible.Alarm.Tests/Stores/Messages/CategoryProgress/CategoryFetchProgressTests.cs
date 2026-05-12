#nullable enable

using Bible.Alarm.Stores.Messages.CategoryProgress;

namespace Bible.Alarm.Tests;

public sealed class CategoryFetchProgressTests
{
    [Fact]
    public void Init_sets_all_members()
    {
        var sut = new CategoryFetchProgress
        {
            CategoryId = 3,
            Progress = 0.125,
            IsComplete = false,
            HasError = true,
        };

        Assert.Equal(3, sut.CategoryId);
        Assert.Equal(0.125, sut.Progress);
        Assert.False(sut.IsComplete);
        Assert.True(sut.HasError);
    }
}
