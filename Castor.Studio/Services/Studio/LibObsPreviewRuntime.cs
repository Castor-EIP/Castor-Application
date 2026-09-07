using CastorApplication.Models.Studio;
using LibObs;

namespace CastorApplication.Services.Studio;

internal sealed class LibObsPreviewRuntime : IDisposable
{
    private ObsDisplay? _previewDisplay;
    private ObsView? _previewView;
    private ObsSource? _previewSceneSource;
    private Guid? _previewSceneId;
    private IntPtr _previewWindowHandle;
    private uint _previewCanvasWidth;
    private uint _previewCanvasHeight;

    public StudioRuntimeResult Start(
        Guid sceneId,
        ObsScene nativeScene,
        IntPtr windowHandle,
        uint width,
        uint height,
        uint canvasWidth,
        uint canvasHeight)
    {
        try
        {
            if (_previewDisplay == null ||
                _previewWindowHandle != windowHandle ||
                _previewSceneId != sceneId)
            {
                Dispose();
                _previewSceneSource = nativeScene.Source;
                _previewView = ObsView.Create();
                _previewView.SetSource(0, _previewSceneSource);
                _previewCanvasWidth = canvasWidth;
                _previewCanvasHeight = canvasHeight;
                _previewDisplay = ObsDisplay.Create(new ObsDisplaySettings
                {
                    WindowHandle = windowHandle,
                    Width = Math.Max(1u, width),
                    Height = Math.Max(1u, height),
                    BackgroundColor = 0xFF000000
                });
                _previewDisplay.AddRenderCallback(RenderPreviewFrame);
                _previewWindowHandle = windowHandle;
            }
            else
            {
                _previewDisplay.Resize(Math.Max(1u, width), Math.Max(1u, height));
            }

            _previewSceneId = sceneId;
            return StudioRuntimeResult.Success();
        }
        catch (Exception exception)
        {
            Dispose();
            return StudioRuntimeResult.Failure(
                $"Démarrage de la preview impossible : {exception.Message}");
        }
    }

    public void Resize(uint width, uint height)
    {
        if (_previewDisplay == null) return;

        try
        {
            _previewDisplay.Resize(width, height);
        }
        catch
        {
            Dispose();
        }
    }

    public void Stop(Guid sceneId)
    {
        if (_previewSceneId == sceneId)
            Dispose();
    }

    public void Dispose()
    {
        var display = _previewDisplay;
        var view = _previewView;
        var sceneSource = _previewSceneSource;
        _previewDisplay = null;
        _previewView = null;
        _previewSceneSource = null;
        _previewSceneId = null;
        _previewWindowHandle = IntPtr.Zero;
        _previewCanvasWidth = 0;
        _previewCanvasHeight = 0;

        try
        {
            display?.Dispose();
        }
        catch
        {
        }

        try
        {
            view?.Dispose();
        }
        catch
        {
        }

        try
        {
            sceneSource?.Dispose();
        }
        catch
        {
        }
    }

    private void RenderPreviewFrame(ObsDisplayFrame frame)
    {
        var sceneSource = _previewSceneSource;
        if (sceneSource == null) return;

        ObsPreviewGraphics.RenderScene(
            frame,
            sceneSource,
            _previewCanvasWidth,
            _previewCanvasHeight);
    }
}
