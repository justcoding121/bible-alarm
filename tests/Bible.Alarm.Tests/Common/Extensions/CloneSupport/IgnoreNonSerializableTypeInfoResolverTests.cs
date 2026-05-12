#nullable enable

using System.IO;
using System.Text.Json;
using Bible.Alarm.Common.Extensions.CloneSupport;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Tests;

public sealed class IgnoreNonSerializableTypeInfoResolverTests
{
    private sealed class PayloadWithDisposable
    {
        public string Name { get; set; } = "";
        public IDisposable? Worker { get; set; }
    }

    private sealed class PayloadWithCommand
    {
        public string Name { get; set; } = "";
        public RelayCommand Save { get; set; } = null!;
    }

    private static JsonSerializerOptions OptionsWithResolver()
    {
        return new JsonSerializerOptions
        {
            TypeInfoResolver = new IgnoreNonSerializableTypeInfoResolver(),
        };
    }

    [Fact]
    public void GetTypeInfo_serializes_without_interface_typed_properties()
    {
        var opts = OptionsWithResolver();
        var original = new PayloadWithDisposable
        {
            Name = "alarm",
            Worker = new MemoryStream(),
        };

        var json = JsonSerializer.Serialize(original, opts);

        var clone = JsonSerializer.Deserialize<PayloadWithDisposable>(json, opts);

        Assert.NotNull(clone);
        Assert.Equal("alarm", clone.Name);
        Assert.Null(clone.Worker);
    }

    [Fact]
    public void GetTypeInfo_serializes_without_ICommand_properties()
    {
        var opts = OptionsWithResolver();
        var original = new PayloadWithCommand
        {
            Name = "go",
            Save = new RelayCommand(() => { }),
        };

        var json = JsonSerializer.Serialize(original, opts);

        var clone = JsonSerializer.Deserialize<PayloadWithCommand>(json, opts);

        Assert.NotNull(clone);
        Assert.Equal("go", clone.Name);
        Assert.Null(clone.Save);
    }
}
