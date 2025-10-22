using System.Collections;
using Bible.Alarm.Common.DataStructures.Shared;

namespace Bible.Alarm.Common.DataStructures;

/// <summary>
/// A sorted HashSet implementation using balanced binary search tree. IEnumerable will enumerate in sorted order.
/// This may be better than regular HashSet implementation which can give o(K) in worst case (but O(1) amortized when collisions K is avoided).
/// </summary>
/// <typeparam name="T">The value datatype.</typeparam>
public sealed class OrderedHashSet<T> : IEnumerable<T> where T : IComparable
{
    //use red-black tree as our balanced BST since it gives good performance for both deletion/insertion
    private readonly RedBlackTree<T> _binarySearchTree;

    public int Count => _binarySearchTree.Count;

    public OrderedHashSet()
    {
        _binarySearchTree = new RedBlackTree<T>();
    }

    /// <summary>
    /// Initialize the sorted hashset with given sorted key collection.
    /// Time complexity: log(n).
    /// </summary>
    public OrderedHashSet(IEnumerable<T> sortedKeys)
    {
        _binarySearchTree = new RedBlackTree<T>(sortedKeys);
    }

    /// <summary>
    ///  Time complexity: O(log(n))
    /// </summary>
    public T this[int index] => ElementAt(index);

    /// <summary>
    /// Does this hash table contains the given value.
    /// Time complexity: O(log(n)).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if this hashset contains the given value.</returns>
    public bool Contains(T value)
    {
        return _binarySearchTree.HasItem(value);
    }

    /// <summary>
    /// Add a new key.
    /// Time complexity: O(log(n)).
    /// Returns the position (index) of the key in sorted order of this OrderedHashSet.
    /// </summary>
    public int Add(T key)
    {
        return _binarySearchTree.Insert(key);
    }

    /// <summary>
    ///  Time complexity: O(log(n))
    /// </summary>
    public T ElementAt(int index)
    {
        return _binarySearchTree.ElementAt(index);
    }

    /// <summary>
    ///  Time complexity: O(log(n))
    /// </summary>
    public int IndexOf(T key)
    {
        return _binarySearchTree.IndexOf(key);
    }

    /// <summary>
    /// Remove the given key if present.
    /// Time complexity: O(log(n)).
    /// Returns the position (index) of the removed key if removed. Otherwise returns -1.
    /// </summary>
    public int Remove(T key)
    {
        return _binarySearchTree.Delete(key);
    }

    /// <summary>
    /// Remove the element at given index.
    /// Time complexity: O(log(n)).
    /// </summary>
    public T RemoveAt(int index)
    {
        return _binarySearchTree.RemoveAt(index);
    }

    /// <summary>
    /// Return the next higher value after given value in this hashset.
    /// Time complexity: O(log(n)).
    /// </summary>
    /// <returns>Null if the given value does'nt exist or next value does'nt exist.</returns>
    public T NextHigher(T value)
    {
        return _binarySearchTree.NextHigher(value);
    }

    /// <summary>
    /// Return the next lower value before given value in this HashSet.
    /// Time complexity: O(log(n)).
    /// </summary>
    /// <returns>Null if the given value does'nt exist or previous value does'nt exist.</returns>
    public T NextLower(T value)
    {
        return _binarySearchTree.NextLower(value);
    }

    /// <summary>
    /// Time complexity: O(log(n)).
    /// </summary>
    public T Max()
    {
        return _binarySearchTree.Max();
    }

    /// <summary>
    /// Time complexity: O(log(n)).
    /// </summary>
    public T Min()
    {
        return _binarySearchTree.Min();
    }

    /// <summary>
    /// Clear the hashtable.
    /// Time complexity: O(1).
    /// </summary>
    internal void Clear()
    {
        _binarySearchTree.Clear();
    }

    /// <summary>
    /// Descending enumerable.
    /// </summary>
    public IEnumerable<T> AsEnumerableDesc()
    {
        return GetEnumeratorDesc().AsEnumerable();
    }

    //Implementation for the GetEnumerator method.
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _binarySearchTree.GetEnumerator();
    }

    public IEnumerator<T> GetEnumeratorDesc()
    {
        return _binarySearchTree.GetEnumeratorDesc();
    }
}