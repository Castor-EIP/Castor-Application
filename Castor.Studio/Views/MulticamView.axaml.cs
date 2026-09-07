using Avalonia.Controls;
using Avalonia.Input;
using CastorApplication.ViewModels.Multicam;

namespace CastorApplication.Views;

public partial class MulticamView : UserControl
{
    public MulticamView()
    {
        InitializeComponent();
    }

    // A gesture belongs to the view. The tile owns what it means: double-clicking a
    // tile is the one deliberate way to put its scene on air from this page.
    private void OnTileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: MulticamSceneTile tile }) return;

        tile.PutOnAirCommand.Execute(null);
        e.Handled = true;
    }
}
