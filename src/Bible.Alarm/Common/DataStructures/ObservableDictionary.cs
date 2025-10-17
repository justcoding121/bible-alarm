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
        private readonly Advanced.Algorithms.DataStructures.Foundation.OrderedDictionary<TKey, TValue> _dictionary = new();

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set => _dictionary[key] = value;
        }

        public int Count => _dictionary.Count;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public int Add(TKey key, TValue value)
        {
            var index = _dictionary.Add(key, value);
            OnNotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new KeyValuePair<TKey, TValue>(key, value), index));
            return index;
        }

        public void Clear()
        {
            _dictionary.Clear();
            OnNotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public int IndexOf(TKey key)
        {
            return _dictionary.IndexOf(key);
        }

        public bool Remove(TKey key)
        {
            if (_dictionary.ContainsKey(key))
            {
                var value = _dictionary[key];
                var index = _dictionary.Remove(key);
                OnNotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new KeyValuePair<TKey, TValue>(key, value), index));
                return true;
            }

            return false;
        }

        public void RemoveAt(int index)
        {
            _dictionary.RemoveAt(index);
        }

        private void OnNotifyCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            CollectionChanged?.Invoke(this, args);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }
    }
}
