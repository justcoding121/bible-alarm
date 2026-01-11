using Android.OS;

namespace CommunityToolkit.Maui.Services;

sealed class BoundServiceBinder(MediaControlsService mediaControlsService) : Binder
{
    public MediaControlsService Service { get; } = mediaControlsService;
}