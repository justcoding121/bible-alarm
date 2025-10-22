using System.Collections;

namespace Bible.Alarm.Common.DataStructures.Shared;

//  implement IEnumerator.
internal class BstEnumerator<T> : IEnumerator<T> where T : IComparable
{
    private readonly bool _asc;

    private readonly BstNodeBase<T> _root;
    private BstNodeBase<T> _current;

    internal BstEnumerator(BstNodeBase<T> root, bool asc = true)
    {
        _root = root;
        _asc = asc;
    }

    public bool MoveNext()
    {
        if (_root == null) return false;

        if (_current == null)
        {
            _current = _asc ? _root.FindMin() : _root.FindMax();
            return true;
        }

        var next = _asc ? _current.NextHigher() : _current.NextLower();
        if (next != null)
        {
            _current = next;
            return true;
        }

        return false;
    }

    public void Reset()
    {
        _current = _root;
    }

    public T Current => _current.Value;

    object IEnumerator.Current => Current;

    public void Dispose()
    {
        _current = null;
    }
}