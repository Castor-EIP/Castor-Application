using System;
using CastorApplication.Models.Studio;

namespace CastorApplication.Services.Studio;

public interface IScenePreviewRuntime
{
    bool IsAvailable { get; }
    string UnavailableMessage { get; }

    event EventHandler? PreviewResetRequested;

    // windowHandle identifies which native surface is asking: the runtime can host one
    // native preview per window at once, so a Studio panel and the Scenes page (or two
    // detached panels) each get their own live display instead of fighting over one.
    Task<StudioRuntimeResult> StartPreviewAsync(
        SceneDefinition scene,
        IntPtr windowHandle,
        uint width,
        uint height,
        CancellationToken cancellationToken);

    void ResizePreview(IntPtr windowHandle, uint width, uint height);

    Task<StudioRuntimeResult> StopPreviewAsync(
        IntPtr windowHandle,
        Guid sceneId,
        CancellationToken cancellationToken);
}
