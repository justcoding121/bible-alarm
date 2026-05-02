#nullable enable

using Bible.Alarm.Common.Extensions;

namespace Bible.Alarm.Tests;

public sealed class CloneExtensionsTests
{
    private sealed class Dto
    {
        public string Name { get; set; } = "";
        public int Id { get; set; }
    }

    [Fact]
    public void DeepClone_copies_values_and_new_instance()
    {
        var original = new Dto { Name = "a", Id = 7 };

        var clone = original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Id, clone.Id);
    }

    [Fact]
    public void DeepClone_throws_when_reference_null()
    {
        Dto? n = null;

        Assert.Throws<ArgumentNullException>(() => n!.DeepClone());
    }

    [Fact]
    public void DeepClone_throws_when_value_is_default_struct()
    {
        Assert.Throws<ArgumentNullException>(() => 0.DeepClone());
    }
}
