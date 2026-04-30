using System.ComponentModel;
using CommunityToolkit.Maui.Converters;
using CommunityToolkit.Maui.Interfaces;
using CommunityToolkit.Maui.Primitives;
using MediaSourceType = CommunityToolkit.Maui.MediaSource.MediaSource;

namespace CommunityToolkit.Maui;

public partial class MediaElement : View, IMediaElement, IDisposable
{
    public static readonly BindableProperty AspectProperty = BindableProperty.Create(nameof(Aspect), typeof(Aspect), typeof(MediaElement), Aspect.AspectFit);
    public static readonly BindableProperty CurrentStateProperty = BindableProperty.Create(nameof(CurrentState), typeof(MediaElementState), typeof(MediaElement), MediaElementState.None, propertyChanged: OnCurrentStatePropertyChanged);

    static readonly BindablePropertyKey durationPropertyKey = BindableProperty.CreateReadOnly(nameof(Duration), typeof(TimeSpan), typeof(MediaElement), TimeSpan.Zero);
    public static readonly BindableProperty DurationProperty = durationPropertyKey.BindableProperty;

    public static readonly BindableProperty ShouldAutoPlayProperty = BindableProperty.Create(nameof(ShouldAutoPlay), typeof(bool), typeof(MediaElement), false);
    public static readonly BindableProperty ShouldLoopPlaybackProperty = BindableProperty.Create(nameof(ShouldLoopPlayback), typeof(bool), typeof(MediaElement), false);
    public static readonly BindableProperty ShouldKeepScreenOnProperty = BindableProperty.Create(nameof(ShouldKeepScreenOn), typeof(bool), typeof(MediaElement), false);
    public static readonly BindableProperty ShouldMuteProperty = BindableProperty.Create(nameof(ShouldMute), typeof(bool), typeof(MediaElement), false);
    public static readonly BindableProperty PositionProperty = BindableProperty.Create(nameof(Position), typeof(TimeSpan), typeof(MediaElement), TimeSpan.Zero);
    public static readonly BindableProperty ShowsPlaybackControlsProperty = BindableProperty.Create(nameof(ShouldShowPlaybackControls), typeof(bool), typeof(MediaElement), true);
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source), typeof(MediaSourceType), typeof(MediaElement), propertyChanging: OnSourcePropertyChanging, propertyChanged: OnSourcePropertyChanged);
    public static readonly BindableProperty SpeedProperty = BindableProperty.Create(nameof(Speed), typeof(double), typeof(MediaElement), 1.0);

    static readonly BindablePropertyKey mediaHeightPropertyKey = BindableProperty.CreateReadOnly(nameof(MediaHeight), typeof(int), typeof(MediaElement), 0);
    public static readonly BindableProperty MediaHeightProperty = mediaHeightPropertyKey.BindableProperty;

    static readonly BindablePropertyKey mediaWidthPropertyKey = BindableProperty.CreateReadOnly(nameof(MediaWidth), typeof(int), typeof(MediaElement), 0);
    public static readonly BindableProperty MediaWidthProperty = mediaWidthPropertyKey.BindableProperty;

    public static readonly BindableProperty VolumeProperty = BindableProperty.Create(nameof(Volume), typeof(double), typeof(MediaElement), 1.0, BindingMode.TwoWay, propertyChanging: ValidateVolume);
    public static readonly BindableProperty MetadataTitleProperty = BindableProperty.Create(nameof(MetadataTitle), typeof(string), typeof(MediaElement), string.Empty);
    public static readonly BindableProperty MetadataArtistProperty = BindableProperty.Create(nameof(MetadataArtist), typeof(string), typeof(MediaElement), string.Empty);
    public static readonly BindableProperty MetadataArtworkUrlProperty = BindableProperty.Create(nameof(MetadataArtworkUrl), typeof(string), typeof(MediaElement), string.Empty);

    readonly WeakEventManager eventManager = new();
    readonly SemaphoreSlim seekToSemaphoreSlim = new(1, 1);

    bool isDisposed;
    IDispatcherTimer? timer;
    TaskCompletionSource seekCompletedTaskCompletionSource = new();

    ~MediaElement() => Dispose(false);

    public event EventHandler MediaEnded
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    public event EventHandler<MediaFailedEventArgs> MediaFailed
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    public event EventHandler MediaOpened
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    public event EventHandler SeekCompleted
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    public event EventHandler<MediaStateChangedEventArgs> StateChanged
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    public event EventHandler<MediaPositionChangedEventArgs> PositionChanged
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    internal event EventHandler StatusUpdated
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    internal event EventHandler PlayRequested
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    internal event EventHandler PauseRequested
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    internal event EventHandler PositionRequested
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    internal event EventHandler<MediaSeekRequestedEventArgs> SeekRequested
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    internal event EventHandler StopRequested
    {
        add => eventManager.AddEventHandler(value);
        remove => eventManager.RemoveEventHandler(value);
    }

    public TimeSpan Position => (TimeSpan)GetValue(PositionProperty);

    public TimeSpan Duration => (TimeSpan)GetValue(DurationProperty);

    public AndroidViewType AndroidViewType { get; init; } = MediaElementOptions.DefaultAndroidViewType;

    public bool ShouldAutoPlay
    {
        get => (bool)GetValue(ShouldAutoPlayProperty);
        set => SetValue(ShouldAutoPlayProperty, value);
    }

    public bool ShouldLoopPlayback
    {
        get => (bool)GetValue(ShouldLoopPlaybackProperty);
        set => SetValue(ShouldLoopPlaybackProperty, value);
    }

    public bool ShouldKeepScreenOn
    {
        get => (bool)GetValue(ShouldKeepScreenOnProperty);
        set => SetValue(ShouldKeepScreenOnProperty, value);
    }

    public bool ShouldMute
    {
        get => (bool)GetValue(ShouldMuteProperty);
        set => SetValue(ShouldMuteProperty, value);
    }

    public bool ShouldShowPlaybackControls
    {
        get => (bool)GetValue(ShowsPlaybackControlsProperty);
        set => SetValue(ShowsPlaybackControlsProperty, value);
    }

    [TypeConverter(typeof(MediaSourceConverter))]
    public MediaSourceType? Source
    {
        get => (MediaSourceType)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double Volume
    {
        get => (double)GetValue(VolumeProperty);
        set
        {
            switch (value)
            {
                case > 1:
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"The value of {nameof(Volume)} cannot be greater than {1}");
                case < 0:
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"The value of {nameof(Volume)} cannot be less than {0}");
                default:
                    SetValue(VolumeProperty, value);
                    break;
            }
        }
    }

    public double Speed
    {
        get => (double)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public int MediaHeight => (int)GetValue(MediaHeightProperty);

    public int MediaWidth => (int)GetValue(MediaWidthProperty);

    public string MetadataTitle
    {
        get => (string)GetValue(MetadataTitleProperty);
        set => SetValue(MetadataTitleProperty, value);
    }

    public string MetadataArtist
    {
        get => (string)GetValue(MetadataArtistProperty);
        set => SetValue(MetadataArtistProperty, value);
    }

    public string MetadataArtworkUrl
    {
        get => (string)GetValue(MetadataArtworkUrlProperty);
        set => SetValue(MetadataArtworkUrlProperty, value);
    }

    public Aspect Aspect
    {
        get => (Aspect)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    public MediaElementState CurrentState
    {
        get => (MediaElementState)GetValue(CurrentStateProperty);
        private set => SetValue(CurrentStateProperty, value);
    }

    TimeSpan IMediaElement.Position
    {
        get => (TimeSpan)GetValue(PositionProperty);
        set
        {
            var currentValue = (TimeSpan)GetValue(PositionProperty);

            if (currentValue != value)
            {
                SetValue(PositionProperty, value);
                OnPositionChanged(new(value));
            }
        }
    }

    TimeSpan IMediaElement.Duration
    {
        get => (TimeSpan)GetValue(DurationProperty);
        set => SetValue(durationPropertyKey, value);
    }

    int IMediaElement.MediaWidth
    {
        get => (int)GetValue(MediaWidthProperty);
        set => SetValue(mediaWidthPropertyKey, value);
    }

    int IMediaElement.MediaHeight
    {
        get => (int)GetValue(MediaHeightProperty);
        set => SetValue(mediaHeightPropertyKey, value);
    }

    TaskCompletionSource IAsynchronousMediaElementHandler.SeekCompletedTcs => seekCompletedTaskCompletionSource;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Pause()
    {
        OnPauseRequested();
        Handler?.Invoke(nameof(PauseRequested));
    }

    public void Play()
    {
        OnPlayRequested();
        Handler?.Invoke(nameof(PlayRequested));
    }

    public async Task SeekTo(TimeSpan position, CancellationToken token = default)
    {
        await seekToSemaphoreSlim.WaitAsync(token);

        try
        {
            MediaSeekRequestedEventArgs args = new(position);
            Handler?.Invoke(nameof(SeekRequested), args);

            await seekCompletedTaskCompletionSource.Task.WaitAsync(token);
        }
        finally
        {
            seekCompletedTaskCompletionSource = new();
            seekToSemaphoreSlim.Release();
        }
    }

    public void Stop()
    {
        OnStopRequested();
        Handler?.Invoke(nameof(StopRequested));
    }

    internal void OnMediaEnded()
    {
        CurrentState = MediaElementState.Stopped;
        eventManager.HandleEvent(this, EventArgs.Empty, nameof(MediaEnded));
    }

    internal void OnMediaFailed(MediaFailedEventArgs args)
    {
        ((IMediaElement)this).Duration = ((IMediaElement)this).Position = TimeSpan.Zero;

        CurrentState = MediaElementState.Failed;
        eventManager.HandleEvent(this, args, nameof(MediaFailed));
    }

    internal void OnMediaOpened()
    {
        InitializeTimer();
        eventManager.HandleEvent(this, EventArgs.Empty, nameof(MediaOpened));
    }

    protected override void OnBindingContextChanged()
    {
        if (Source is not null)
        {
            SetInheritedBindingContext(Source, BindingContext);
        }

        base.OnBindingContextChanged();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            ClearTimer();
            seekToSemaphoreSlim.Dispose();
        }

        isDisposed = true;
    }

    static void OnSourcePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((MediaElement)bindable).OnSourcePropertyChanged((MediaSourceType?)newValue);
    }

    static void OnSourcePropertyChanging(BindableObject bindable, object oldValue, object newValue)
    {
        ((MediaElement)bindable).OnSourcePropertyChanging((MediaSourceType?)oldValue);
    }

    static void OnCurrentStatePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var mediaElement = (MediaElement)bindable;
        var previousState = (MediaElementState)oldValue;
        var newState = (MediaElementState)newValue;

        mediaElement.OnStateChanged(new MediaStateChangedEventArgs(previousState, newState));
    }

    static void ValidateVolume(BindableObject bindable, object oldValue, object newValue)
    {
        var updatedVolume = (double)newValue;

        if (updatedVolume is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(newValue), $"{nameof(Volume)} can not be less than 0.0 or greater than 1.0");
        }
    }

    void OnTimerTick(object? sender, EventArgs e)
    {
        OnPositionRequested();
        OnUpdateStatus();
        Handler?.Invoke(nameof(StatusUpdated));
    }

    void InitializeTimer()
    {
        if (timer is not null)
        {
            return;
        }

        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(200);
        timer.Tick += OnTimerTick;
        timer.Start();
    }

    void ClearTimer()
    {
        if (timer is null)
        {
            return;
        }

        timer.Tick -= OnTimerTick;
        timer.Stop();
        timer = null;
    }

    void OnSourceChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(SourceProperty.PropertyName);
        InvalidateMeasure();
    }

    void OnSourcePropertyChanged(MediaSourceType? newValue)
    {
        ClearTimer();

        if (newValue is not null)
        {
            newValue.SourceChanged += OnSourceChanged;
            SetInheritedBindingContext(newValue, BindingContext);
        }

        InvalidateMeasure();
        InitializeTimer();
    }

    void OnSourcePropertyChanging(MediaSourceType? oldValue)
    {
        if (oldValue is null)
        {
            return;
        }

        oldValue.SourceChanged -= OnSourceChanged;
    }

    void IMediaElement.MediaEnded()
    {
        OnMediaEnded();
    }

    void IMediaElement.MediaFailed(MediaFailedEventArgs args)
    {
        OnMediaFailed(args);
    }

    void IMediaElement.MediaOpened()
    {
        OnMediaOpened();
    }

    void IMediaElement.SeekCompleted()
    {
        OnSeekCompleted();
    }

    void IMediaElement.CurrentStateChanged(MediaElementState newState)
    {
        CurrentState = newState;
    }

    void OnPositionChanged(MediaPositionChangedEventArgs mediaPositionChangedEventArgs)
    {
        eventManager.HandleEvent(this, mediaPositionChangedEventArgs, nameof(PositionChanged));
    }

    void OnStateChanged(MediaStateChangedEventArgs mediaStateChangedEventArgs)
    {
        eventManager.HandleEvent(this, mediaStateChangedEventArgs, nameof(StateChanged));
    }

    void OnPauseRequested()
    {
        eventManager.HandleEvent(this, EventArgs.Empty, nameof(PauseRequested));
    }

    void OnPlayRequested()
    {
        eventManager.HandleEvent(this, EventArgs.Empty, nameof(PlayRequested));
    }

    void OnStopRequested()
    {
        eventManager.HandleEvent(this, EventArgs.Empty, nameof(StopRequested));
    }

    void OnSeekCompleted()
    {
        eventManager.HandleEvent(this, EventArgs.Empty, nameof(SeekCompleted));
    }

    void OnPositionRequested()
    {
        eventManager.HandleEvent(this, EventArgs.Empty, nameof(PositionRequested));
    }

    void OnUpdateStatus()
    {
        eventManager.HandleEvent(this, EventArgs.Empty, nameof(StatusUpdated));
    }
}
