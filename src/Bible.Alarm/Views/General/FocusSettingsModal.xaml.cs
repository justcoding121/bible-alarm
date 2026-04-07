#nullable enable

using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Views.General;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class FocusSettingsModal : BaseContentPage
{
    private readonly Action? onDismissed;

    public FocusSettingsModal(Action? onDismissed = null)
    {
        InitializeComponent();
        this.onDismissed = onDismissed;

        var gotItTap = new TapGestureRecognizer();
        gotItTap.Tapped += OnGotItTapped;
        GotItButton.GestureRecognizers.Add(gotItTap);

        var remindTap = new TapGestureRecognizer();
        remindTap.Tapped += OnRemindLaterTapped;
        RemindLaterButton.GestureRecognizers.Add(remindTap);
    }

    private async void OnGotItTapped(object? sender, EventArgs e)
    {
        HomeViewModelFocusWarningHandler.Dismiss();
        onDismissed?.Invoke();
        await Navigation.PopModalAsync();
    }

    private async void OnRemindLaterTapped(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
