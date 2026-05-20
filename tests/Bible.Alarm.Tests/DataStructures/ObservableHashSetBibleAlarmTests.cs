#nullable enable

using System.Collections;
using System.Collections.Specialized;
using Bible.Alarm.Shared.DataStructures;

namespace Bible.Alarm.Tests;

public sealed class ObservableHashSetBibleAlarmTests
{
    [Fact]
    public void Contains_reports_membership_against_underlying_sorted_set()
    {
        var set = new ObservableHashSet<int>();
        set.Add(2);
        Assert.True(set.Contains(2));
        Assert.False(set.Contains(3));
        Assert.False(set.IsReadOnly);
        Assert.False(set.IsSynchronized);
        Assert.Same(set, set.SyncRoot);
    }

    [Fact]
    public void Remove_returns_false_without_notification_when_item_missing()
    {
        var notifications = 0;
        var set = new ObservableHashSet<int>();
        set.CollectionChanged += (_, _) => notifications++;

        Assert.False(set.Remove(42));

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Generic_enumerator_yields_sorted_members()
    {
        var set = new ObservableHashSet<int>();
        set.Add(3);
        set.Add(1);

        Assert.Equal([1, 3], set.ToArray());
    }

    [Fact]
    public void NonGeneric_IEnumerable_iterations_yield_sorted_members()
    {
        var set = new ObservableHashSet<int>();
        set.Add(40);
        set.Add(-1);
        set.Add(5);
        IEnumerable raw = set;
        Assert.Equal([-1, 5, 40], raw.Cast<int>().ToArray());

        var enumerator = raw.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(-1, enumerator.Current);
    }

    [Fact]
    public void Add_RaisesAddNotification_WhenNewItemInserted()
    {
        var observed = new List<NotifyCollectionChangedEventArgs>();
        var set = new ObservableHashSet<int>();
        set.CollectionChanged += (_, args) => observed.Add(args);

        set.Add(5);

        Assert.Single(observed);
        Assert.Equal(NotifyCollectionChangedAction.Add, observed[0].Action);
        Assert.Equal(5, observed[0].NewItems![0]);
    }

    [Fact]
    public void Remove_RaisesRemoveNotification_WhenItemExists()
    {
        NotifyCollectionChangedEventArgs? last = null;
        var set = new ObservableHashSet<int>();
        set.Add(10);
        set.CollectionChanged += (_, args) => last = args;

        Assert.True(set.Remove(10));

        Assert.NotNull(last);
        Assert.Equal(NotifyCollectionChangedAction.Remove, last!.Action);
    }

    [Fact]
    public void Clear_RaisesReset()
    {
        NotifyCollectionChangedEventArgs? last = null;
        var set = new ObservableHashSet<int>();
        set.Add(3);
        set.CollectionChanged += (_, args) => last = args;

        set.Clear();

        Assert.NotNull(last);
        Assert.Equal(NotifyCollectionChangedAction.Reset, last!.Action);
    }

    [Fact]
    public void CopyTo_and_ElementAt_cover_sorted_storage_paths()
    {
        var set = new ObservableHashSet<int>();
        set.Add(9);
        set.Add(1);
        var target = new int[3];
        set.CopyTo(target, 1);
        Assert.Equal(1, target[1]);
        Assert.Equal(9, target[2]);
        Assert.Equal(1, set.ElementAt(0));
        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[1], 0));
    }

    [Fact]
    public void NonGeneric_ICollection_CopyTo_validates_arguments_and_boxes_values()
    {
        var set = new ObservableHashSet<int>();
        set.Add(40);
        set.Add(2);
        ICollection nonGeneric = set;

        Assert.Throws<ArgumentNullException>(() => nonGeneric.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => nonGeneric.CopyTo(new object?[2], -1));
        Assert.Throws<ArgumentException>(() => nonGeneric.CopyTo(new object?[1], 1));

        var destination = new object?[4];
        nonGeneric.CopyTo(destination, 2);
        Assert.Equal(2, destination[2]);
        Assert.Equal(40, destination[3]);
    }
}
