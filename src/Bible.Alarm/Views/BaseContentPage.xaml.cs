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
            var grid = new Grid
            {
                RowDefinitions =
                [
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(GridLength.Auto)
                ]
            };

            var presenter = new ContentPresenter();
            Grid.SetRow(presenter, 0);
            grid.Add(presenter);

            var miniBar = new MiniPlaybackBar();
            Grid.SetRow(miniBar, 1);
            grid.Add(miniBar);

            return grid;
        });
    }
}
