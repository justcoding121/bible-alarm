#nullable enable

using Bible.Alarm.Platforms.Windows.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Maui;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsSmtcServiceTests
{
    private sealed class StubMediaElementService : IMediaElementService
    {
        private readonly MediaElement? mediaElement;

        public StubMediaElementService(MediaElement? mediaElement = null) =>
            this.mediaElement = mediaElement;

        public Task<MediaElement> GetMediaElementAsync() =>
            Task.FromResult(mediaElement ?? new MediaElement());

        public Task DisposeMediaElementAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    [Fact]
    public void UpdateButtonStates_is_no_op_before_initialize()
    {
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
        var sut = new WindowsSmtcService(new StubMediaElementService(new MediaElement()));

        await sut.InitializeAsync();
    }

    [Fact]
    public async Task InitializeAsync_second_call_is_idempotent()
    {
        var sut = new WindowsSmtcService(new StubMediaElementService(new MediaElement()));

        await sut.InitializeAsync();
        await sut.InitializeAsync();
    }

    [Fact]
    public void UpdateButtonStates_after_initialize_does_not_throw()
    {
        var sut = new WindowsSmtcService(new StubMediaElementService(new MediaElement()));

        sut.InitializeAsync().GetAwaiter().GetResult();

        var ex = Record.Exception(() => sut.UpdateButtonStates(canPlayNext: false, canPlayPrevious: true));

        Assert.Null(ex);
        sut.Dispose();
    }
}
