#nullable enable

namespace Bible.Alarm.Shared.Media;

/// <summary>
/// App-defined playback kinds that the Android proxy player exposes to a media session.
/// Stopped and Ended are not separate kinds: the Fluxor effect already maps both to paused.
/// </summary>
public enum ProxyPlaybackKind
{
    None = 0,
    Buffering,
    Playing,
    Paused,
    Error,
}

/// <summary>
/// How a proxy kind is shown to Android Auto. Names match Media3 player states.
/// </summary>
public readonly record struct ProxyPlaybackMapping(string StateName, bool PlayWhenReady, bool HasPlayerError)
{
    public static ProxyPlaybackMapping For(ProxyPlaybackKind kind) => kind switch
    {
        ProxyPlaybackKind.Playing => new("READY", PlayWhenReady: true, HasPlayerError: false),
        ProxyPlaybackKind.Buffering => new("BUFFERING", PlayWhenReady: true, HasPlayerError: false),
        ProxyPlaybackKind.Paused => new("READY", PlayWhenReady: false, HasPlayerError: false),
        ProxyPlaybackKind.Error => new("IDLE", PlayWhenReady: false, HasPlayerError: true),
        _ => new("IDLE", PlayWhenReady: false, HasPlayerError: false),
    };
}
