using CastorApplication.Services.Ai;
using CastorApplication.ViewModels.Multicam;
using CastorApplication.ViewModels.Studio;

namespace Castor.Studio.Tests;

public sealed class MulticamAiFocusTests
{
    private static MulticamViewModel CreateViewModel(StudioWorkspaceViewModel workspace) =>
        new(new UnavailableAiAnalysisClient(), workspace);

    [Fact]
    public void Tiles_start_with_no_ai_marking()
    {
        var workspace = new StudioWorkspaceViewModel();
        var viewModel = CreateViewModel(workspace);
        workspace.CreateScene("Plateau");

        var tile = Assert.Single(viewModel.Tiles);
        Assert.Equal(MulticamAiState.None, tile.AiState);
        Assert.False(tile.IsAiSelected);
        Assert.False(tile.IsAiConsidered);
    }

    [Fact]
    public void Setting_the_focus_marks_the_pick_and_its_candidates()
    {
        var workspace = new StudioWorkspaceViewModel();
        var viewModel = CreateViewModel(workspace);
        var wide = workspace.CreateScene("Plateau");
        var closeup = workspace.CreateScene("Caméra");
        var spare = workspace.CreateScene("Public");

        viewModel.SetAiFocus(closeup, [wide]);

        Assert.Equal(MulticamAiState.Selected, viewModel.Tiles.Single(t => t.Scene == closeup).AiState);
        Assert.Equal(MulticamAiState.Considered, viewModel.Tiles.Single(t => t.Scene == wide).AiState);
        Assert.Equal(MulticamAiState.None, viewModel.Tiles.Single(t => t.Scene == spare).AiState);
    }

    [Fact]
    public void A_new_focus_replaces_the_previous_one_entirely()
    {
        var workspace = new StudioWorkspaceViewModel();
        var viewModel = CreateViewModel(workspace);
        var wide = workspace.CreateScene("Plateau");
        var closeup = workspace.CreateScene("Caméra");

        viewModel.SetAiFocus(wide, [closeup]);
        viewModel.SetAiFocus(closeup);

        // The grid must never keep showing a decision the pipeline moved on from.
        Assert.Equal(MulticamAiState.None, viewModel.Tiles.Single(t => t.Scene == wide).AiState);
        Assert.Equal(MulticamAiState.Selected, viewModel.Tiles.Single(t => t.Scene == closeup).AiState);
    }

    [Fact]
    public void Clearing_the_focus_drops_every_marking()
    {
        var workspace = new StudioWorkspaceViewModel();
        var viewModel = CreateViewModel(workspace);
        var wide = workspace.CreateScene("Plateau");
        var closeup = workspace.CreateScene("Caméra");
        viewModel.SetAiFocus(wide, [closeup]);

        viewModel.ClearAiFocus();

        Assert.All(viewModel.Tiles, tile => Assert.Equal(MulticamAiState.None, tile.AiState));
    }

    [Fact]
    public void The_marking_survives_a_scene_being_added()
    {
        var workspace = new StudioWorkspaceViewModel();
        var viewModel = CreateViewModel(workspace);
        var wide = workspace.CreateScene("Plateau");
        viewModel.SetAiFocus(wide);

        workspace.CreateScene("Caméra");

        Assert.Equal(MulticamAiState.Selected, viewModel.Tiles.Single(t => t.Scene == wide).AiState);
    }

    [Fact]
    public void A_scene_that_leaves_the_workspace_stops_being_the_pick()
    {
        var workspace = new StudioWorkspaceViewModel();
        var viewModel = CreateViewModel(workspace);
        var wide = workspace.CreateScene("Plateau");
        var closeup = workspace.CreateScene("Caméra");
        viewModel.SetAiFocus(wide, [closeup]);

        workspace.DeleteScene(wide);

        Assert.Equal(MulticamAiState.Considered, viewModel.Tiles.Single(t => t.Scene == closeup).AiState);
        Assert.DoesNotContain(viewModel.Tiles, tile => tile.IsAiSelected);
    }

    [Fact]
    public void The_ai_marking_is_independent_of_what_is_on_air()
    {
        var workspace = new StudioWorkspaceViewModel();
        var viewModel = CreateViewModel(workspace);
        var onAir = workspace.CreateScene("Plateau");
        var wanted = workspace.CreateScene("Caméra");
        workspace.SelectScene(onAir);

        viewModel.SetAiFocus(wanted);

        // The gap between what is broadcast and what the AI wants is the thing an
        // operator needs to see, so the two markings must not collapse into one.
        var onAirTile = viewModel.Tiles.Single(t => t.Scene == onAir);
        var wantedTile = viewModel.Tiles.Single(t => t.Scene == wanted);
        Assert.True(onAirTile.IsOnAir);
        Assert.False(onAirTile.IsAiSelected);
        Assert.False(wantedTile.IsOnAir);
        Assert.True(wantedTile.IsAiSelected);
    }
}
