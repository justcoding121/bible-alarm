#nullable enable

namespace Bible.Alarm.Views.General;

public partial class PlaybackModalLandscapeContent
{
    public PlaybackModalLandscapeContent()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Progress slider for landscape overlay; used by parent for iOS tap-to-seek and value/drag events.
    /// </summary>
    public Slider? ProgressSlider => LandscapeProgressSlider;

    /// <summary>
    /// Stop button in landscape bar; used by parent to attach Clicked handler.
    /// </summary>
    public Button? StopButton => LandscapeStopButton;

    /// <summary>
    /// Minimize button container; used by parent to adjust margin for Android status bar offset.
    /// </summary>
    public Grid? MinimizeContainer => LandscapeMinimizeContainer;
}
