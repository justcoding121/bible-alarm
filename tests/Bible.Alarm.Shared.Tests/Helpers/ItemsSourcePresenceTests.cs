#nullable enable

using System.Collections;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class ItemsSourcePresenceTests
{
    private sealed class ThrowingEnumerable : IEnumerable
    {
        public IEnumerator GetEnumerator() => throw new InvalidOperationException();
    }

    [Fact]
    public void HasAnyItems_returns_false_when_enumerator_allocation_throws()
    {
        Assert.False(ItemsSourcePresence.HasAnyItems(new ThrowingEnumerable()));
    }

    [Fact]
    public void ContainsItem_returns_false_when_iteration_throws_before_match()
    {
        Assert.False(ItemsSourcePresence.ContainsItem(new ThrowingEnumerable(), new object()));
    }

    [Fact]
    public void ContainsItem_matches_structurally_equal_strings_when_reference_differs()
    {
        object src = new List<string> { "alpha" };

        Assert.True(ItemsSourcePresence.ContainsItem(src, new string("alpha".ToCharArray())));
    }

    [Fact]
    public void HasAnyItems_true_when_ICollection_has_positive_count_even_if_IEnumerable_empty()
    {
        object weird = new WeirdCollection();

        Assert.True(ItemsSourcePresence.HasAnyItems(weird));
    }

    private sealed class WeirdCollection : ICollection
    {
        public int Count => 3;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public void CopyTo(Array array, int index) =>
            throw new NotSupportedException();

        public IEnumerator GetEnumerator()
        {
            yield break;
        }
    }
}
