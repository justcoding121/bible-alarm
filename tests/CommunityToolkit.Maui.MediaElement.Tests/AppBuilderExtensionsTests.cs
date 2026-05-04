#nullable enable

using CommunityToolkit.Maui;

namespace CommunityToolkit.Maui.MediaElement.Tests;

public sealed class AppBuilderExtensionsTests
{
    [Fact]
    public void UseMauiCommunityToolkitMediaElement_invokes_configuration_and_returns_same_builder()
    {
        var builder = MauiApp.CreateBuilder();

        MediaElementOptions? passedToCallback = null;
        var returned = builder.UseMauiCommunityToolkitMediaElement(o => passedToCallback = o);

        Assert.Same(builder, returned);
        Assert.NotNull(passedToCallback);
    }

    [Fact]
    public void UseMauiCommunityToolkitMediaElement_without_callback_returns_same_builder()
    {
        var builder = MauiApp.CreateBuilder();

        var returned = builder.UseMauiCommunityToolkitMediaElement();

        Assert.Same(builder, returned);
    }
}
