#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class AssemblyWarmupHelperTests
{
    [Fact]
    public async Task WarmupHttpAssembliesAsync_completes_without_throwing()
    {
        await AssemblyWarmupHelper.WarmupHttpAssembliesAsync();
    }
}
