using System.Collections;
using System.Collections.Specialized;

namespace Bible.Alarm.Common.DataStructures;

public sealed class ObservableHashSet<T> : INotifyCollectionChanged,
    ICollection<T>,
    IEnumerable,
    ICollection where T : IComparable
{
    private readonly OrderedHashSet<T> _sortedHashSet = [];

    public int Count => _sortedHashSet.Count;

    public bool IsReadOnly => false;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    public event NotifyCollectionChangedEventHandler CollectionChanged;

    public void Add(T item)
    {
        AddItem(item);
    }

    private int AddItem(T item)
    {
        var index = _sortedHashSet.Add(item);
        OnNotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        return index;
    }

    public void Clear()
    {
        _sortedHashSet.Clear();
        OnNotifyCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public bool Contains(T item)
    {
        return _sortedHashSet.Contains(item);
    }

    public bool Remove(T item)
    {
        var index = _sortedHashSet.Remove(item);
        if (index >= 0)
        {
            OnNotifyCollectionChanged(
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            return true;
        }

        return false;
    }

    public T ElementAt(int index)
    {
        return _sortedHashSet[index];
    }

    public int IndexOf(T item)
    {
        return _sortedHashSet.IndexOf(item);
    }

    private void OnNotifyCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        CollectionChanged?.Invoke(this, args);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count) throw new ArgumentException("Array is too small");
        
        var index = 0;
        foreach (var item in _sortedHashSet)
        {
            array[arrayIndex + index] = item;
            index++;
        }
    }

    public void CopyTo(Array array, int index)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (array.Length - index < Count) throw new ArgumentException("Array is too small");
        
        var i = 0;
        foreach (var item in _sortedHashSet)
        {
            array.SetValue(item, index + i);
            i++;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _sortedHashSet.GetEnumerator();
    }
}