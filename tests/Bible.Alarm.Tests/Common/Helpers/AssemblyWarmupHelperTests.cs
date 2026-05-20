#nullable enable

using System.Reflection;
using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class AssemblyWarmupHelperTests
{
    private sealed class ThrowingStaticCtorType
    {
        static ThrowingStaticCtorType() => throw new InvalidOperationException("warmup");
    }

    [Fact]
    public async Task WarmupHttpAssembliesAsync_completes_without_throwing()
    {
        await AssemblyWarmupHelper.WarmupHttpAssembliesAsync();
    }

    [Fact]
    public async Task WarmupHttpAssembliesAsync_can_be_called_multiple_times()
    {
        await AssemblyWarmupHelper.WarmupHttpAssembliesAsync();
        await AssemblyWarmupHelper.WarmupHttpAssembliesAsync();
    }

    [Fact]
    public void WarmupType_swallows_type_initializer_errors_via_reflection()
    {
        var method = typeof(AssemblyWarmupHelper).GetMethod(
            "WarmupType",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var generic = method!.MakeGenericMethod(typeof(ThrowingStaticCtorType));
        var ex = Record.Exception(() => generic.Invoke(null, null));

        Assert.Null(ex);
    }
}
