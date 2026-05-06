#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class MultiBoolOrConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    private readonly MultiBoolOrConverter sut = new();

    [Fact]
    public void Empty_or_null_values_yield_false()
    {
        Assert.False((bool)sut.Convert([], typeof(bool), null!, Cul));
        Assert.False((bool)sut.Convert(Array.Empty<object>(), typeof(bool), null!, Cul));
        Assert.False((bool)sut.Convert(null!, typeof(bool), null!, Cul));
    }

    [Fact]
    public void True_if_any_bound_value_is_true()
    {
        var result = sut.Convert([false, false, true, false], typeof(bool), null!, Cul);

        Assert.True((bool)result);
    }

    [Fact]
    public void Inverse_parameter_negates_when_any_true_or_default_branch()
    {
        Assert.False((bool)sut.Convert([true], typeof(bool), "Inverse", Cul));
        Assert.True((bool)sut.Convert([false], typeof(bool), "Negate", Cul));
    }

    [Fact]
    public void All_false_with_inverse_parameter_yields_true()
    {
        Assert.True((bool)sut.Convert([false, false], typeof(bool), "Inverse", Cul));
    }

    [Fact]
    public void All_false_without_parameter_yields_false()
    {
        Assert.False((bool)sut.Convert([false, false], typeof(bool), null!, Cul));
    }

    [Fact]
    public void Null_elements_skipped_until_first_true()
    {
        Assert.True((bool)sut.Convert([null!, false, true], typeof(bool), null!, Cul));
    }

    [Fact]
    public void ConvertBack_throws()
    {
        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(false, [typeof(bool)], null!, Cul));
    }
}
