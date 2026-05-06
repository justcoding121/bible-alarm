#nullable enable

using Bible.Alarm.Common.Extensions;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Tests;

public sealed class CloneExtensionsTests
{
    private sealed class Dto
    {
        public string Name { get; set; } = "";
        public int Id { get; set; }
    }

    private sealed class DtoWithCommand
    {
        public string Name { get; set; } = "";
        public RelayCommand Save { get; set; } = null!;
    }

    private sealed class ParentDto
    {
        public Dto Child { get; set; } = new();
    }

    [Fact]
    public void DeepClone_copies_nested_dto_graph()
    {
        var original = new ParentDto { Child = new Dto { Name = "nested", Id = 42 } };

        var clone = original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.Child, clone.Child);
        Assert.Equal(original.Child.Name, clone.Child.Name);
        Assert.Equal(original.Child.Id, clone.Child.Id);
    }

    [Fact]
    public void DeepClone_copies_list_elements()
    {
        var original = new List<Dto>
        {
            new() { Name = "first", Id = 1 },
            new() { Name = "second", Id = 2 },
        };

        var clone = original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.Equal(2, clone.Count);
        Assert.NotSame(original[0], clone[0]);
        Assert.Equal("second", clone[1].Name);
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

    [Fact]
    public void DeepClone_omits_command_like_properties_via_type_info_resolver()
    {
        var original = new DtoWithCommand
        {
            Name = "alarm",
            Save = new RelayCommand(() => { }),
        };

        var clone = original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Name, clone.Name);
        Assert.Null(clone.Save);
    }
}
