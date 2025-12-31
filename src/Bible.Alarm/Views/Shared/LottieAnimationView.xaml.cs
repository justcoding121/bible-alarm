#nullable enable
using System.Timers;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Bible.Alarm.Common.Helpers;
using System.Reflection;

namespace Bible.Alarm.Views.Shared;

public partial class LottieAnimationView : SKCanvasView
{
    private System.Timers.Timer? animationTimer;
    private float currentFrame = 0f;
    private bool isAnimating = false;
    private readonly object lockObject = new object();
    private LottieAnimationData? lottieData;
    private bool isLottieLoaded = false;

    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(
        nameof(IsRunning),
        typeof(bool),
        typeof(LottieAnimationView),
        false,
        BindingMode.OneWay,
        propertyChanged: OnIsRunningChanged);

    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color),
        typeof(Color),
        typeof(LottieAnimationView),
        Colors.Blue,
        BindingMode.OneWay,
        propertyChanged: OnColorChanged);

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private static void OnIsRunningChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is LottieAnimationView view)
        {
            var isRunning = (bool)newValue;
            if (isRunning)
            {
                view.StartAnimation();
            }
            else
            {
                view.StopAnimation();
            }
        }
    }

    private static void OnColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is LottieAnimationView view)
        {
            view.InvalidateSurface();
        }
    }

    public LottieAnimationView()
    {
        InitializeComponent();
        LoadLottieAnimation();
    }

    private void LoadLottieAnimation()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = ResourceLoader.GetEmbeddedResourceStream(assembly, "loading_spinner.json");
            lottieData = LottieAnimationData.Parse(stream);
            isLottieLoaded = lottieData != null;
        }
        catch (Exception ex)
        {
            // If Lottie loading fails, fall back to custom spinner
            System.Diagnostics.Debug.WriteLine($"Failed to load Lottie animation: {ex.Message}");
            isLottieLoaded = false;
        }
    }

    private void StartAnimation()
    {
        lock (lockObject)
        {
            if (isAnimating) return;
            isAnimating = true;
            currentFrame = 0f;

            // Use Lottie frame rate if available, otherwise default to 60 FPS
            var frameRate = lottieData?.FrameRate ?? 60;
            var intervalMs = 1000.0 / frameRate;

            // Use a timer that runs independently of the UI thread
            // This ensures smooth animation even when UI thread is blocked
            animationTimer = new System.Timers.Timer(intervalMs);
            animationTimer.Elapsed += OnTimerElapsed;
            animationTimer.AutoReset = true;
            animationTimer.Start();
        }
    }

    private void StopAnimation()
    {
        lock (lockObject)
        {
            if (!isAnimating) return;
            isAnimating = false;

            animationTimer?.Stop();
            animationTimer?.Dispose();
            animationTimer = null;
        }
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Update animation frame on timer thread (not UI thread)
        lock (lockObject)
        {
            if (lottieData != null)
            {
                // Use Lottie animation frame range
                currentFrame += 1f;
                if (currentFrame >= lottieData.OutPoint)
                {
                    currentFrame = lottieData.InPoint; // Loop back to start
                }
            }
            else
            {
                // Fallback: rotate 6 degrees per frame at 60 FPS
                currentFrame += 6f;
                if (currentFrame >= 360f)
                {
                    currentFrame -= 360f;
                }
            }
        }

        // Invalidate surface on UI thread to trigger redraw
        // This is thread-safe and will queue the redraw
        MainThread.BeginInvokeOnMainThread(() =>
        {
            InvalidateSurface();
        });
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var surface = e.Surface;
        var canvas = surface.Canvas;
        canvas.Clear();

        var info = e.Info;
        var scaleX = info.Width / (float)(lottieData?.Width ?? 60);
        var scaleY = info.Height / (float)(lottieData?.Height ?? 60);
        var scale = Math.Min(scaleX, scaleY);

        if (isLottieLoaded && lottieData != null && lottieData.Layers.Length > 0)
        {
            // Render using Lottie animation data
            RenderLottieAnimation(canvas, info, scale);
        }
        else
        {
            // Fallback to custom spinner
            RenderCustomSpinner(canvas, info);
        }
    }

    private void RenderLottieAnimation(SKCanvas canvas, SKImageInfo info, float scale)
    {
        if (lottieData == null || lottieData.Layers.Length == 0)
            return;

        var layer = lottieData.Layers[0];
        
        // Calculate rotation from keyframes
        float rotation = 0f;
        if (layer.RotationKeyframes.Length >= 2)
        {
            var startFrame = layer.RotationKeyframes[0].Frame;
            var endFrame = layer.RotationKeyframes[^1].Frame;
            var startValue = layer.RotationKeyframes[0].Value;
            var endValue = layer.RotationKeyframes[^1].Value;

            // Linear interpolation
            if (endFrame > startFrame)
            {
                var progress = (currentFrame - startFrame) / (endFrame - startFrame);
                rotation = startValue + (endValue - startValue) * progress;
            }
            else
            {
                rotation = startValue;
            }
        }

        // Get circle properties from Lottie data
        var circleWidth = layer.CircleWidth * scale;
        var circleHeight = layer.CircleHeight * scale;
        var centerX = info.Width / 2f;
        var centerY = info.Height / 2f;

        // Get stroke properties
        var strokeWidth = layer.StrokeWidth * scale;
        
        // Always use the Color property to allow theme customization
        // The Lottie file color is ignored in favor of the bindable Color property
        var color = Color.ToSKColor();

        // Draw the circle with rotation
        canvas.Save();
        canvas.Translate(centerX, centerY);
        canvas.RotateDegrees(rotation);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
            Color = color
        };

        var rect = new SKRect(-circleWidth / 2f, -circleHeight / 2f, circleWidth / 2f, circleHeight / 2f);
        canvas.DrawOval(rect, paint);

        canvas.Restore();
    }

    private void RenderCustomSpinner(SKCanvas canvas, SKImageInfo info)
    {
        var centerX = info.Width / 2f;
        var centerY = info.Height / 2f;
        var radius = Math.Min(info.Width, info.Height) / 2f - 10f;

        // Draw rotating spinner with multiple arcs (fallback)
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4f,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        // Draw 8 arcs that create a spinner effect
        for (int i = 0; i < 8; i++)
        {
            var angle = currentFrame + (i * 45f);
            var startAngle = angle;
            var sweepAngle = 30f; // Each arc spans 30 degrees

            // Calculate opacity based on position (fade effect)
            var opacity = 1.0f - (i * 0.1f);
            if (opacity < 0.2f) opacity = 0.2f;

            var skColor = Color.ToSKColor();
            paint.Color = new SKColor(skColor.Red, skColor.Green, skColor.Blue, (byte)(skColor.Alpha * opacity));

            var rect = new SKRect(
                centerX - radius,
                centerY - radius,
                centerX + radius,
                centerY + radius);

            using var path = new SKPath();
            path.AddArc(rect, startAngle, sweepAngle);
            canvas.DrawPath(path, paint);
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (IsRunning)
        {
            StartAnimation();
        }
    }
}

