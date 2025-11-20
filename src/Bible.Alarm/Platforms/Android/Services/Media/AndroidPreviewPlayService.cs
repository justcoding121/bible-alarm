using Android.Media;
using Bible.Alarm.Common.Interfaces.Media;
using AndroidApplication = Android.App.Application;
using AndroidNet = Android.Net;
using Object = Java.Lang.Object;


namespace Bible.Alarm.Platforms.Android.Services.Media;

public class AndroidPreviewPlayService(MediaPlayer player) : Object,
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
        var uri = AndroidNet.Uri.Parse(url);
        _player.Reset();
        _player.SetOnCompletionListener(this);
        _player.SetDataSource(AndroidApplication.Context, uri);
        _player.Prepare();
        _player.Start();

        return Task.CompletedTask;
    }

    private bool _disposed;

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