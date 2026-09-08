namespace CastorApplication.ViewModels.Multicam;

/// <summary>
/// What the AI pipeline is doing with a scene right now, as far as the grid
/// has been told.
/// </summary>
public enum MulticamAiState
{
    /// <summary>The AI is not looking at this scene.</summary>
    None,

    /// <summary>A candidate the AI is weighing, but has not picked.</summary>
    Considered,

    /// <summary>The scene the AI would switch to.</summary>
    Selected,
}
