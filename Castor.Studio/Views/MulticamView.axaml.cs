using Avalonia.Controls;
using Avalonia.Input;
using CastorApplication.ViewModels.Multicam;

namespace CastorApplication.Views;

public partial class MulticamView : UserControl
{
    // The tile being dragged. Held here rather than in the drag payload because the
    // payload only carries data, and the drop needs the tile itself.
    private MulticamSceneTile? _draggedTile;

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

    // The band is the drag handle, and it has to be: the preview below it is a native
    // child window that takes the mouse events over the video, so a drag started there
    // would never reach us. A title bar is where one grabs a panel anyway.
    private async void OnTileBandPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: MulticamSceneTile tile }) return;
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;

        _draggedTile = tile;

        // The payload is a formality: the drop needs the tile itself, which is held
        // above, not something a data format can carry.
        var payload = new DataTransfer();
        payload.Add(DataTransferItem.CreateText(tile.Scene.Id.ToString()));

        try
        {
            await DragDrop.DoDragDropAsync(e, payload, DragDropEffects.Move);
        }
        finally
        {
            _draggedTile = null;
        }
    }

    private void OnTileDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = _draggedTile != null ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnTileDrop(object? sender, DragEventArgs e)
    {
        if (_draggedTile == null) return;
        if (sender is not Control { DataContext: MulticamSceneTile target }) return;
        if (DataContext is not MulticamViewModel viewModel) return;

        viewModel.MoveScene(_draggedTile.Scene, target.Scene);
        e.Handled = true;
    }
}
