namespace CastorApplication.ViewModels.Multicam;

/// <summary>How the multicam page arranges its scenes.</summary>
public enum MulticamLayout
{
    /// <summary>Every scene the same size, filling the page.</summary>
    Grid,

    /// <summary>The scene on air shown large, the rest as a strip beneath it.</summary>
    Spotlight,
}
