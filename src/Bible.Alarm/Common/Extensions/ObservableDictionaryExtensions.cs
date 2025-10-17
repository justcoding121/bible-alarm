using Bible.Alarm.Common.DataStructures;
using System;
using System.Collections.Generic;

namespace Bible.Alarm
{
    public static class ObservableDictionaryExtensions
    {
        public static ObservableDictionary<TKey, TElement> ToObservableDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector) where TKey : IComparable
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");
            if (elementSelector == null) throw new ArgumentNullException("elementSelector");
            var d = new ObservableDictionary<TKey, TElement>();
            foreach (TSource element in source)
            {
                d.Add(keySelector(element), elementSelector(element));
            }
            return d;
        }


    }
}
