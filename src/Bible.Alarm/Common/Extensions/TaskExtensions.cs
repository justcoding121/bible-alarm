using System.Runtime.CompilerServices;

namespace Bible.Alarm.Common.Extensions;

public static class TaskExtensions
{
    public static ConfiguredTaskAwaitable ContinueOnAnyContext(this Task @this)
    {
        return @this.ConfigureAwait(false);
    }

    public static ConfiguredTaskAwaitable<T> ContinueOnAnyContext<T>(this Task<T> @this)
    {
        return @this.ConfigureAwait(false);
    }
}