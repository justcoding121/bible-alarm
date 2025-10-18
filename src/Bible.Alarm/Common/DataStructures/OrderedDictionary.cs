using System.Collections;

namespace Advanced.Algorithms.DataStructures.Foundation;

/// <summary>
/// A sorted Dictionary implementation using balanced binary search tree. IEnumerable will enumerate in sorted order.
/// This may be better than regular Dictionary implementation which can give o(K) in worst case (but O(1) amortized when collisions K is avoided).
/// </summary>
/// <typeparam name="K">The key datatype.</typeparam>
/// <typeparam name="V">The value datatype.</typeparam>
public class OrderedDictionary<K, V> : IEnumerable<KeyValuePair<K, V>> where K : IComparable
{
    //use red-black tree as our balanced BST since it gives good performance for both deletion/insertion
    private readonly RedBlackTree<OrderedKeyValuePair<K, V>> _binarySearchTree;

    public int Count => _binarySearchTree.Count;

    public OrderedDictionary()
    {
        _binarySearchTree = new RedBlackTree<OrderedKeyValuePair<K, V>>(true);
    }

    /// <summary>
    /// Initialize the dictionary with given key value pairs sorted by key.
    /// Time complexity: log(n).
    /// </summary>
    public OrderedDictionary(IEnumerable<KeyValuePair<K, V>> sortedKeyValuePairs)
    {
        _binarySearchTree =
            new RedBlackTree<OrderedKeyValuePair<K, V>>(sortedKeyValuePairs.Select(x =>
                new OrderedKeyValuePair<K, V>(x.Key, x.Value)));
    }

    /// <summary>
    /// Does this dictionary contains the given key.
    /// Time complexity: O(log(n)).
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if this dictionary contains the given key.</returns> 
    public bool ContainsKey(K key)
    {
        return _binarySearchTree.HasItem(new OrderedKeyValuePair<K, V>(key, default));
    }

    /// <summary>
    /// Add a new value for given key.
    /// Time complexity: O(log(n)).
    /// Returns the position (index) of the key in sorted order of this OrderedDictionary.
    /// </summary>
    public int Add(K key, V value)
    {
        return _binarySearchTree.Insert(new OrderedKeyValuePair<K, V>(key, value));
    }

    /// <summary>
    /// Get/set value for given key.
    /// Time complexity: O(log(n)).
    /// </summary>
    public V this[K key]
    {
        get
        {
            var node = _binarySearchTree.FindNode(new OrderedKeyValuePair<K, V>(key, default));
            if (node == null) throw new Exception("Key not found.");

            return node.Value.Value;
        }
        set
        {
            if (ContainsKey(key)) Remove(key);

            Add(key, value);
        }
    }

    /// <summary>
    ///  Time complexity: O(log(n))
    /// </summary>
    public KeyValuePair<K, V> ElementAt(int index)
    {
        return _binarySearchTree.ElementAt(index).ToKeyValuePair();
    }

    /// <summary>
    ///  Time complexity: O(log(n))
    /// </summary>
    public int IndexOf(K key)
    {
        return _binarySearchTree.IndexOf(new OrderedKeyValuePair<K, V>(key, default));
    }

    /// <summary>
    /// Remove the given key if it exists.
    /// Time complexity: O(log(n)).
    /// Returns the position (index) of the removed key if removed. Otherwise returns -1.
    /// </summary>
    public int Remove(K key)
    {
        return _binarySearchTree.Delete(new OrderedKeyValuePair<K, V>(key, default));
    }

    /// <summary>
    /// Remove the element at given index.
    /// Time complexity: O(log(n)).
    /// </summary>
    public KeyValuePair<K, V> RemoveAt(int index)
    {
        return _binarySearchTree.RemoveAt(index).ToKeyValuePair();
    }

    /// <summary>
    /// Return the next higher key-value pair after given key in this dictionary.
    /// Time complexity: O(log(n)).
    /// </summary>
    /// <returns>Null if the given key does'nt exist or next key does'nt exist.</returns>
    public KeyValuePair<K, V> NextHigher(K key)
    {
        var next = _binarySearchTree.NextHigher(new OrderedKeyValuePair<K, V>(key, default));

        if (next.Equals(default(OrderedKeyValuePair<K, V>))) return default;

        return new KeyValuePair<K, V>(next.Key, next.Value);
    }

    /// <summary>
    /// Return the next lower key-value pair before given key in this dictionary.
    /// Time complexity: O(log(n)).
    /// </summary>
    /// <returns>Null if the given key does'nt exist or previous key does'nt exist.</returns>
    public KeyValuePair<K, V> NextLower(K key)
    {
        var prev = _binarySearchTree.NextLower(new OrderedKeyValuePair<K, V>(key, default));

        if (prev.Equals(default(OrderedKeyValuePair<K, V>))) return default;

        return new KeyValuePair<K, V>(prev.Key, prev.Value);
    }

    /// <summary>
    /// Time complexity: O(1).
    /// </summary>
    public KeyValuePair<K, V> Max()
    {
        var max = _binarySearchTree.Max();
        return max.Equals(default(OrderedKeyValuePair<K, V>))
            ? default
            : max.ToKeyValuePair();
    }

    /// <summary>
    /// Time complexity: O(log(n)).
    /// </summary>
    public KeyValuePair<K, V> Min()
    {
        var min = _binarySearchTree.Min();
        return min.Equals(default(OrderedKeyValuePair<K, V>))
            ? default
            : min.ToKeyValuePair();
    }


    /// <summary>
    /// Clear the dictionary.
    /// Time complexity: O(log(n)).
    /// </summary>
    internal void Clear()
    {
        _binarySearchTree.Clear();
    }

    /// <summary>
    /// Descending enumerable.
    /// </summary>
    public IEnumerable<KeyValuePair<K, V>> AsEnumerableDesc()
    {
        return GetEnumeratorDesc().AsEnumerable();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
    {
        return new SortedDictionaryEnumerator<K, V>(_binarySearchTree);
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumeratorDesc()
    {
        return new SortedDictionaryEnumerator<K, V>(_binarySearchTree, false);
    }
}

internal struct OrderedKeyValuePair<K, V> : IComparable
    where K : IComparable
{
    internal K Key { get; }
    internal V Value { get; set; }

    internal OrderedKeyValuePair(K key, V value)
    {
        Key = key;
        Value = value;
    }

    public KeyValuePair<K, V> ToKeyValuePair()
    {
        return new KeyValuePair<K, V>(Key, Value);
    }

    public int CompareTo(object obj)
    {
        if (obj is OrderedKeyValuePair<K, V> itemToComare) return Key.CompareTo(itemToComare.Key);

        throw new ArgumentException("Compare object is nu");
    }

    public override bool Equals(object obj)
    {
        return Key.Equals(((OrderedKeyValuePair<K, V>)obj).Key);
    }

    public override int GetHashCode()
    {
        return Key.GetHashCode();
    }
}

internal class SortedDictionaryEnumerator<K, V> : IEnumerator<KeyValuePair<K, V>> where K : IComparable
{
    private RedBlackTree<OrderedKeyValuePair<K, V>> _bst;
    private IEnumerator<OrderedKeyValuePair<K, V>> _enumerator;

    internal SortedDictionaryEnumerator(RedBlackTree<OrderedKeyValuePair<K, V>> bst, bool asc = true)
    {
        _bst = bst;
        _enumerator = asc ? bst.GetEnumerator() : bst.GetEnumeratorDesc();
    }

    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    public void Reset()
    {
        _enumerator.Reset();
    }

    object IEnumerator.Current => Current;

    public KeyValuePair<K, V> Current => new(_enumerator.Current.Key, _enumerator.Current.Value);

    public void Dispose()
    {
        _bst = null;
        _enumerator = null;
    }
}