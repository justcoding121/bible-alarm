using Android.Media;
using Bible.Alarm.Contracts.Media;

// using Bible.Alarm.Services.Droid.Extensions; // Removed - no longer needed


namespace Bible.Alarm.Platforms.Android.Services.Media;

public class PreviewPlayService(MediaPlayer player) : Java.Lang.Object,
    MediaPlayer.IOnCompletionListener, IPreviewPlayService, IDisposable
{
    private MediaPlayer _player = player;

    public event Action OnStopped;

    public void Stop()
    {
        _player.Stop();
    }

    public void OnCompletion(MediaPlayer mp)
    {
        OnStopped?.Invoke();
    }

    Task IPreviewPlayService.Play(string url)
    {
        var uri = Android.Net.Uri.Parse(url);
        _player.Reset();
        _player.SetOnCompletionListener(this);
        _player.SetDataSource(Android.App.Application.Context, uri);
        _player.Prepare();
        _player.Start();

        return Task.CompletedTask;
    }

    private bool _disposed = false;

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;

        _player?.Stop();
        _player?.Dispose();
        _player = null;

        _disposed = true;
        base.Dispose(disposing);
    }
}