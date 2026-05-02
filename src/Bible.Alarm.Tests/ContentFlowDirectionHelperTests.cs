#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

namespace Bible.Alarm.Tests;

public sealed class ContentFlowDirectionHelperTests
{
    [Theory]
    [InlineData("rtl")]
    [InlineData("RTL")]
    [InlineData("Rtl")]
    public void Rtl_direction_maps_to_RightToLeft(string direction)
    {
        Assert.Equal(FlowDirection.RightToLeft,
            ContentFlowDirectionHelper.GetContentFlowDirection(direction));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ltr")]
    [InlineData("unknown")]
    public void Non_rtl_maps_to_LeftToRight(string? direction)
    {
        Assert.Equal(FlowDirection.LeftToRight,
            ContentFlowDirectionHelper.GetContentFlowDirection(direction));
    }

    [Fact]
    public void Rtl_constant_matches_expected_literal()
    {
        Assert.Equal("rtl", AppConstants.Media.TextDirectionRightToLeft);
    }
}
