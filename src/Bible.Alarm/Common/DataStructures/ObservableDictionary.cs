using Advanced.Algorithms.DataStructures.Foundation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Bible.Alarm.Common.DataStructures
{
    public class ObservableDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>,
                                                        IEnumerable,
                                                        INotifyCollectionChanged
                                                            where TKey : IComparable
    {
        private readonly OrderedDictionary<TKey, TValue> dictionary;

        public ObservableDictionary()
        {
            dictionary = new OrderedDictionary<TKey, TValue>();
        }

        public TValue this[TKey key]
        {
            get => dictionary[key];
            set => dictionary[key] = value;
        }

        public int Count => dictionary.Count;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public int Add(TKey key, TValue value)
        {
            var index = dictionary.Add(key, value);
            onNotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new KeyValuePair<TKey, TValue>(key, value), index));
            return index;
        }

        public void Clear()
        {
            dictionary.Clear();
            onNotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool ContainsKey(TKey key)
        {
            return dictionary.ContainsKey(key);
        }

        public int IndexOf(TKey key)
        {
            return dictionary.IndexOf(key);
        }

        public bool Remove(TKey key)
        {
            if (dictionary.ContainsKey(key))
            {
                var value = dictionary[key];
                var index = dictionary.Remove(key);
                onNotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new KeyValuePair<TKey, TValue>(key, value), index));
                return true;
            }

            return false;
        }

        public void RemoveAt(int index)
        {
            dictionary.RemoveAt(index);
        }

        private void onNotifyCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            CollectionChanged?.Invoke(this, args);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }
    }
}
