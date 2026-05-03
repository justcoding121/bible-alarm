using Bible.Alarm.Shared.DataStructures;
using System.Collections;
using System.Collections.Specialized;

namespace Bible.Alarm.Shared.Tests;

public sealed class ObservableHashSetTests
{
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
    public void Add_DoesNotNotify_WhenDuplicate()
    {
        var count = 0;
        var set = new ObservableHashSet<int>();
        set.CollectionChanged += (_, _) => count++;
        set.Add(1);
        set.Add(1);
        Assert.Equal(1, count);
        Assert.Single(set);
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
        Assert.Equal(10, last.OldItems![0]);
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
    public void IndexOf_ReturnsSortedPosition()
    {
        var set = new ObservableHashSet<int>();
        set.Add(30);
        set.Add(10);
        set.Add(20);
        Assert.Equal(1, set.IndexOf(20));
        Assert.Equal(-1, set.IndexOf(99));
    }

    [Fact]
    public void CopyTo_Throws_WhenDestinationTooSmall()
    {
        var set = new ObservableHashSet<int>();
        set.Add(1);
        set.Add(2);
        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[1], 0));
    }

    [Fact]
    public void CopyTo_CopiesSortedOrderIntoArray()
    {
        var set = new ObservableHashSet<int>();
        set.Add(9);
        set.Add(1);
        var target = new int[3];
        set.CopyTo(target, 1);
        Assert.Equal(0, target[0]);
        Assert.Equal(1, target[1]);
        Assert.Equal(9, target[2]);
    }

    [Fact]
    public void ElementAt_ReturnsItemAtSortedZeroBasedIndex()
    {
        var set = new ObservableHashSet<int>();
        set.Add(3);
        set.Add(1);
        Assert.Equal(1, set.ElementAt(0));
        Assert.Equal(3, set.ElementAt(1));
    }

    [Fact]
    public void AsNonGeneric_ICollection_CopyTo_Boxes_sorted_values_and_reports_metadata()
    {
        var set = new ObservableHashSet<int>();
        set.Add(40);
        set.Add(2);
        ICollection nonGeneric = set;

        Assert.False(nonGeneric.IsSynchronized);
        Assert.Same(nonGeneric, nonGeneric.SyncRoot);
        Assert.Equal(2, nonGeneric.Count);

        var destination = new object?[4];
        nonGeneric.CopyTo(destination, 2);

        Assert.Equal(2, destination[2]);
        Assert.Equal(40, destination[3]);
        Assert.Null(destination[1]);
    }
}
