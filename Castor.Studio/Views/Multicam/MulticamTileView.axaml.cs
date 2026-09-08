using Avalonia.Controls;
using Avalonia.Input;
using CastorApplication.ViewModels.Multicam;

namespace CastorApplication.Views.Multicam;

// One scene's tile: the video, its band, and its chrome. Lives on its own so the grid and
// the dock can both show it without either owning the markup.
public partial class MulticamTileView : UserControl
{
    private static MulticamSceneTile? _draggedTile;

    public MulticamTileView()
    {
        InitializeComponent();
    }

    // A gesture belongs to the view. The tile owns what it means: double-clicking a tile is
    // the one deliberate way to put its scene on air from this page.
    private void OnTileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MulticamSceneTile tile) return;

        tile.PutOnAirCommand.Execute(null);
        e.Handled = true;
    }

    // The band is the drag handle, and it has to be: the preview below it is a native child
    // window that takes the mouse events over the video, so a drag started there would never
    // reach us. A title bar is where one grabs a panel anyway.
    private async void OnTileBandPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MulticamSceneTile tile) return;
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;

        _draggedTile = tile;

        // The payload is a formality: the drop needs the tile itself, held above, not
        // something a data format can carry.
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
        if (DataContext is not MulticamSceneTile target) return;
        target.MoveSceneHere(_draggedTile.Scene);
        e.Handled = true;
    }
}
