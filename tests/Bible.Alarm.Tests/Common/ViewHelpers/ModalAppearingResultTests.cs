#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class ModalAppearingResultTests
{
    [Fact]
    public void Enum_values_cover_modal_outcome_surface()
    {
        Assert.Equal(0, (int)ModalAppearingResult.Success);
        Assert.Equal(1, (int)ModalAppearingResult.FetchFailed);
        Assert.Equal(2, (int)ModalAppearingResult.Cancelled);
    }
}
