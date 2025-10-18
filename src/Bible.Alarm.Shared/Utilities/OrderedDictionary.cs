using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Bible.Alarm.Shared.Utilities;

public class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    private readonly List<KeyValuePair<TKey, TValue>> _items = new();

    public OrderedDictionary()
    {
    }

    public OrderedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        _items.AddRange(items);
    }

    public TValue this[TKey key]
    {
        get
        {
            var item = _items.FirstOrDefault(x => EqualityComparer<TKey>.Default.Equals(x.Key, key));
            if (item.Equals(default(KeyValuePair<TKey, TValue>)))
                throw new KeyNotFoundException();
            return item.Value;
        }
        set
        {
            var index = _items.FindIndex(x => EqualityComparer<TKey>.Default.Equals(x.Key, key));
            if (index >= 0)
                _items[index] = new KeyValuePair<TKey, TValue>(key, value);
            else
                _items.Add(new KeyValuePair<TKey, TValue>(key, value));
        }
    }

    public ICollection<TKey> Keys => _items.Select(x => x.Key).ToList();
    public ICollection<TValue> Values => _items.Select(x => x.Value).ToList();
    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        if (ContainsKey(key))
            throw new ArgumentException("An element with the same key already exists.");
        _items.Add(new KeyValuePair<TKey, TValue>(key, value));
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    public void Clear()
    {
        _items.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return _items.Contains(item);
    }

    public bool ContainsKey(TKey key)
    {
        return _items.Any(x => EqualityComparer<TKey>.Default.Equals(x.Key, key));
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        _items.CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    public bool Remove(TKey key)
    {
        var index = _items.FindIndex(x => EqualityComparer<TKey>.Default.Equals(x.Key, key));
        if (index >= 0)
        {
            _items.RemoveAt(index);
            return true;
        }

        return false;
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        return _items.Remove(item);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        var item = _items.FirstOrDefault(x => EqualityComparer<TKey>.Default.Equals(x.Key, key));
        if (!item.Equals(default(KeyValuePair<TKey, TValue>)))
        {
            value = item.Value;
            return true;
        }

        value = default;
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}