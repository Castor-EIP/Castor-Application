using CastorApplication.Models.Studio;

namespace CastorApplication.Services.Ai;

internal interface IAiAnalysisClient : IAsyncDisposable
{
    bool IsAvailable { get; }
    string UnavailableMessage { get; }
    bool HasActiveSession { get; }
    string? SessionId { get; }
    IReadOnlySet<Guid> ActiveSceneIds { get; }

    event EventHandler<AiSceneSwitchEvent>? SceneSwitchSuggested;
    event EventHandler<AiSessionStatusEvent>? SessionStatusChanged;
    event EventHandler<AiServerErrorEvent>? ServerErrorReceived;

    Task StartSessionAsync(string moduleName, string mode, IReadOnlyList<SceneDefinition> scenes,
        CancellationToken cancellationToken);
    Task UpdateSourcesAsync(IReadOnlyList<SceneDefinition> scenes, CancellationToken cancellationToken);
    Task StopSessionAsync(string reason, CancellationToken cancellationToken);
}
