namespace System.Threading.Tasks
{
    using Runtime.CompilerServices;

    public static class TaskExtensions
    {
        public static ConfiguredTaskAwaitable ContinueOnAnyContext(this Task @this)
        {
            return @this.ConfigureAwait(continueOnCapturedContext: false);
        }

        public static ConfiguredTaskAwaitable<T> ContinueOnAnyContext<T>(this Task<T> @this)
        {
            return @this.ConfigureAwait(continueOnCapturedContext: false);
        }
    }
}