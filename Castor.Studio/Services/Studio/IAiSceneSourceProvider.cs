using LibObs;

namespace CastorApplication.Services.Studio;

/// <summary>Supplies a strong reference to a scene source for an auxiliary output.</summary>
internal interface IAiSceneSourceProvider
{
    bool IsAvailable { get; }
    ObsSource? AcquireSceneSource(Guid sceneId);
}
