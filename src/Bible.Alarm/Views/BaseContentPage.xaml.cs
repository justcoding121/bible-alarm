#nullable enable
using Bible.Alarm.Views.Shared;

namespace Bible.Alarm.Views;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BaseContentPage : ContentPage
{
    public BaseContentPage()
    {
        InitializeComponent();

        ControlTemplate = new ControlTemplate(() =>
        {
            // Two-row Grid: content fills Row 0 (*), mini bar occupies Row 1 (Auto).
            // When the mini bar is hidden (IsVisible=false) the Auto row collapses to zero
            // so content fills the full height. When visible, content is pushed up and
            // nothing is hidden behind the bar.
            var grid = new Grid
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(GridLength.Auto)
                }
            };

            var presenter = new ContentPresenter
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill
            };
            Grid.SetRow(presenter, 0);
            grid.Add(presenter);

            var miniBar = new MiniPlaybackBar();
            Grid.SetRow(miniBar, 1);
            grid.Add(miniBar);

            return grid;
        });
    }
}
