using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class ConcurrencyHelperTests
{
    [Fact]
    public async Task ExecuteWithTimeoutAsync_returns_value_when_lock_is_free()
    {
        using var semaphore = new SemaphoreSlim(1, 1);
        var result = await ConcurrencyHelper.ExecuteWithTimeoutAsync(
            semaphore,
            async () =>
            {
                await Task.CompletedTask;
                return 42;
            },
            timeoutMs: 5000);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteWithTimeoutAsync_returns_null_when_lock_is_held_until_timeout()
    {
        using var semaphore = new SemaphoreSlim(1, 1);
        await semaphore.WaitAsync(5000);
        try
        {
            var result = await ConcurrencyHelper.ExecuteWithTimeoutAsync(
                semaphore,
                async () =>
                {
                    await Task.CompletedTask;
                    return 99;
                },
                timeoutMs: 50);

            Assert.Null(result);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
