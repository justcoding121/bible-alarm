#nullable enable

using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionRefreshContextTests
{
    [Fact]
    public async Task All_members_invoke_passed_hooks()
    {
        var disposedCalls = 0;
        var selectingCalls = 0;
        bool? busySeen = null;
        bool? cancelSeen = null;
        bool? progressVisible = null;
        string? progressText = null;
        double? pct = null;
        bool? screenOn = null;
        var initializeCalls = 0;
        var selectedCalls = 0;
        var initFalseCalls = 0;

        var ctx = new BiblePublicationSectionRefreshContext
        {
            IsDisposed = () =>
            {
                disposedCalls++;
                return false;
            },
            IsSelectingSection = () =>
            {
                selectingCalls++;
                return true;
            },
            SetIsBusy = b => busySeen ??= b,
            SetCanCancelFetch = b => cancelSeen ??= b,
            SetShowProgress = b => progressVisible ??= b,
            SetProgressText = s => progressText ??= s,
            SetProgressPercent = d => pct ??= d,
            SetScreenOn = b => screenOn ??= b,
            Initialize = (_, _, _) =>
            {
                initializeCalls++;
                return Task.CompletedTask;
            },
            SetSelectedSection = () => selectedCalls++,
            SetInitCompleteFalse = () => initFalseCalls++,
        };

        Assert.False(ctx.IsDisposed());
        Assert.True(ctx.IsSelectingSection());
        ctx.SetIsBusy(true);
        ctx.SetCanCancelFetch(false);
        ctx.SetShowProgress(true);
        ctx.SetProgressText("x");
        ctx.SetProgressPercent(0.5);
        ctx.SetScreenOn(true);

        await ctx.Initialize("lc", "pc", null);

        ctx.SetSelectedSection();
        ctx.SetInitCompleteFalse();

        Assert.Equal(1, disposedCalls);
        Assert.Equal(1, selectingCalls);
        Assert.True(busySeen);
        Assert.False(cancelSeen);
        Assert.True(progressVisible);
        Assert.Equal("x", progressText);
        Assert.Equal(0.5, pct);
        Assert.True(screenOn);
        Assert.Equal(1, initializeCalls);
        Assert.Equal(1, selectedCalls);
        Assert.Equal(1, initFalseCalls);
    }
}
