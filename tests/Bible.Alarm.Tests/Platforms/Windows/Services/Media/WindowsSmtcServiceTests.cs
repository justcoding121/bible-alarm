#nullable enable

using Bible.Alarm.Platforms.Windows.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using CommunityToolkit.Maui;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
[Collection("MauiUi")]
public sealed class WindowsSmtcServiceTests(MauiUiFixture fixture)
{
    private sealed class StubMediaElementService : IMediaElementService
    {
        private MediaElement? mediaElement;

        public Task<MediaElement> GetMediaElementAsync()
        {
            mediaElement ??= new MediaElement();
            return Task.FromResult(mediaElement);
        }

        public Task DisposeMediaElementAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    [Fact]
    public void UpdateButtonStates_is_no_op_before_initialize()
    {
        _ = fixture;
        var sut = new WindowsSmtcService(new StubMediaElementService());

        var ex = Record.Exception(() => sut.UpdateButtonStates(canPlayNext: true, canPlayPrevious: true));

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_is_safe_when_not_initialized()
    {
        var sut = new WindowsSmtcService(new StubMediaElementService());

        var ex = Record.Exception(() => sut.Dispose());

        Assert.Null(ex);
    }

    [Fact]
    public async Task InitializeAsync_completes_without_throw_when_media_element_has_no_handler()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new WindowsSmtcService(new StubMediaElementService());

        await sut.InitializeAsync();
    }

    [Fact]
    public async Task InitializeAsync_second_call_is_idempotent()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new WindowsSmtcService(new StubMediaElementService());

        await sut.InitializeAsync();
        await sut.InitializeAsync();
    }

    [Fact]
    public async Task UpdateButtonStates_after_initialize_does_not_throw()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new WindowsSmtcService(new StubMediaElementService());

        await sut.InitializeAsync();

        var ex = Record.Exception(() => sut.UpdateButtonStates(canPlayNext: false, canPlayPrevious: true));

        Assert.Null(ex);
        sut.Dispose();
    }
}
