namespace Bible.Alarm.Common.DataStructures.Shared;

internal static class IEnumerableExtensions
{
    internal static IEnumerable<T> AsEnumerable<T>(this IEnumerator<T> e)
    {
        while (e.MoveNext()) yield return e.Current;
    }
}