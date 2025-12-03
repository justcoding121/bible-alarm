#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using static Serilog.Log;

namespace Bible.Alarm.Views.Schedule;

public partial class Schedule : BaseContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly ScheduleViewModel _viewModel;

    public ScheduleViewModel? ViewModel => BindingContext as ScheduleViewModel;

    private bool _hasHandledFirstLoad;

    public Schedule(ScheduleViewModel viewModel)
    {
        var constructorStartTime = DateTime.UtcNow;
        try { Log.Information("[PERF] Schedule page: Constructor started at {StartTime}", constructorStartTime); } catch { }
        
        var initComponentStartTime = DateTime.UtcNow;
        InitializeComponent();
        var initComponentElapsed = (DateTime.UtcNow - initComponentStartTime).TotalMilliseconds;
        try { Log.Information("[PERF] Schedule page: InitializeComponent took {ElapsedMs}ms", initComponentElapsed); } catch { }
        
        BindingContext = viewModel;
        _viewModel = viewModel;

        // Setup gesture recognizers after page is loaded to support hot reload
        Loaded += OnPageLoaded;
        Loaded += SetupGestureRecognizers;
        
        var constructorElapsed = (DateTime.UtcNow - constructorStartTime).TotalMilliseconds;
        try { Log.Information("[PERF] Schedule page: Constructor completed in {ElapsedMs}ms", constructorElapsed); } catch { }
    }

    private void SetupGestureRecognizers(object? sender, EventArgs e)
    {
        // Only setup once per page instance
        if (MusicButton?.GestureRecognizers.Count == 0)
        {
            MusicButton?.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => AnimateUtils.FlickUponTouched(MusicButton, 1500,
                    ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
            });
        }

        if (BibleButton?.GestureRecognizers.Count == 0)
        {
            BibleButton?.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => AnimateUtils.FlickUponTouched(BibleButton, 1500,
                    ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
            });
        }

        // Unsubscribe after setup
        Loaded -= SetupGestureRecognizers;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Only handle once per page instance
        if (_hasHandledFirstLoad) return;
        _hasHandledFirstLoad = true;

        // Unsubscribe to avoid multiple calls
        Loaded -= OnPageLoaded;

        // Wait a bit to ensure the page is fully rendered and visible
        // Loaded fires after the page is in the visual tree, but we still need a small delay
        // to ensure all UI elements are fully rendered
        await Task.Delay(100);
        
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Hide Home page overlay after Schedule page is fully rendered and visible
            _viewModel?.HideHomePageOverlay();
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset flag when page appears again (e.g., navigating back to it)
        _hasHandledFirstLoad = false;
        Loaded += OnPageLoaded;
    }


    protected override bool OnBackButtonPressed()
    {
        ViewModel?.CancelCommand.Execute(null);
        return true;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // ViewModel was injected via constructor, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _isDisposed = true;
        }
    }
}