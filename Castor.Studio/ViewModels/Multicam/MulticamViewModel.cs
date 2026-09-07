using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CastorApplication.Models.Settings;
using CastorApplication.Services.Ai;
using CastorApplication.Services.Settings;
using CastorApplication.Services.Studio;
using CastorApplication.ViewModels.Scenes;
using CastorApplication.ViewModels.Studio;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CastorApplication.ViewModels.Multicam;

/// <summary>
/// One cell of the multicam grid: a scene, rendered live, that can also be
/// ticked for the AI to reason about.
/// </summary>
public sealed partial class MulticamSceneTile : ViewModelBase
{
    private readonly StudioWorkspaceViewModel _workspace;

    public SceneItemViewModel Scene { get; }
    public string Name => Scene.Name;
    public int SourceCount => Scene.Sources.Count;

    // Each tile draws on its own native surface. LibObsSceneRuntime keeps one
    // preview session per window, so every tile renders at the same time
    // instead of taking turns - which is the whole point of a grid.
    public IScenePreviewRuntime PreviewRuntime { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _baseCanvasWidth = 1920;
    [ObservableProperty] private int _baseCanvasHeight = 1080;

    /// <summary>Whether this is the scene currently going to the output.</summary>
    public bool IsOnAir => ReferenceEquals(_workspace.ActiveScene, Scene);

    public string PreviewPlaceholderText => !PreviewRuntime.IsAvailable
        ? PreviewRuntime.UnavailableMessage
        : !StudioWorkspaceViewModel.HasVideoSource(Scene)
            ? "Pas de source vidéo"
            : "";

    internal MulticamSceneTile(SceneItemViewModel scene, StudioWorkspaceViewModel workspace,
        IScenePreviewRuntime previewRuntime, int baseCanvasWidth, int baseCanvasHeight)
    {
        Scene = scene;
        _workspace = workspace;
        PreviewRuntime = previewRuntime;
        BaseCanvasWidth = baseCanvasWidth;
        BaseCanvasHeight = baseCanvasHeight;
        Scene.PropertyChanged += OnScenePropertyChanged;
        Scene.Sources.CollectionChanged += OnSourcesChanged;
    }

    internal void NotifyOnAirChanged() => OnPropertyChanged(nameof(IsOnAir));

    internal void ApplyBaseCanvas(int width, int height)
    {
        BaseCanvasWidth = width;
        BaseCanvasHeight = height;
    }

    private void OnScenePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SceneItemViewModel.Name)) OnPropertyChanged(nameof(Name));
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SourceCount));
        OnPropertyChanged(nameof(PreviewPlaceholderText));
    }
}

public partial class MulticamViewModel : ViewModelBase
{
    private readonly IAiAnalysisClient _aiAnalysisClient;
    private readonly StudioWorkspaceViewModel _workspace;
    private readonly IScenePreviewRuntime _previewRuntime;
    private readonly SettingsService? _settingsService;

    public ObservableCollection<SceneItemViewModel> Scenes => _workspace.Scenes;

    /// <summary>The grid itself: one live tile per scene in the workspace.</summary>
    public ObservableCollection<MulticamSceneTile> Tiles { get; } = [];

    public bool HasScenes => Tiles.Count > 0;

    [ObservableProperty] private bool _isAiOff = true;
    [ObservableProperty] private bool _isAiAgent;
    [ObservableProperty] private bool _isAiAuto;
    [ObservableProperty] private int _selectedAiModelIndex;
    [ObservableProperty] private string _aiStatusText = "IA désactivée";
    [ObservableProperty] private string _aiError = "";
    [ObservableProperty] private bool _isAiBusy;

    public bool IsAiEnabled => !IsAiOff;

    internal MulticamViewModel(
        IAiAnalysisClient aiAnalysisClient,
        StudioWorkspaceViewModel workspace,
        IScenePreviewRuntime? previewRuntime = null,
        SettingsService? settingsService = null)
    {
        _aiAnalysisClient = aiAnalysisClient;
        _workspace = workspace;
        _previewRuntime = previewRuntime ?? new UnavailableScenePreviewRuntime();
        _settingsService = settingsService;
        RefreshTiles();
        Scenes.CollectionChanged += (_, _) => RefreshTiles();
        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
        if (_settingsService != null)
            _settingsService.SettingsSaved += OnSettingsSaved;
    }

    [RelayCommand]
    private void RefreshTiles()
    {
        // Ticks survive a rebuild: a scene added elsewhere must not silently
        // drop what the operator had already picked for the AI.
        var selectedIds = Tiles.Where(tile => tile.IsSelected).Select(tile => tile.Scene.Id).ToHashSet();
        var (width, height) = CurrentBaseCanvas();

        Tiles.Clear();
        foreach (var scene in Scenes)
        {
            Tiles.Add(new MulticamSceneTile(scene, _workspace, _previewRuntime, width, height)
            {
                IsSelected = selectedIds.Contains(scene.Id),
            });
        }

        OnPropertyChanged(nameof(HasScenes));
    }

    private (int Width, int Height) CurrentBaseCanvas()
    {
        var resolution = VideoResolution.BaseFromIndex(_settingsService?.Load().SelectedBaseResolutionIndex ?? 1);
        return (resolution.Width, resolution.Height);
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(StudioWorkspaceViewModel.ActiveScene)) return;

        foreach (var tile in Tiles) tile.NotifyOnAirChanged();
    }

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        var (width, height) = CurrentBaseCanvas();
        foreach (var tile in Tiles) tile.ApplyBaseCanvas(width, height);
    }

    [RelayCommand]
    private void SetAiOff()
    {
        IsAiOff = true;
        IsAiAgent = false;
        IsAiAuto = false;
        AiError = "";
        AiStatusText = "IA désactivée";
        OnPropertyChanged(nameof(IsAiEnabled));
    }

    [RelayCommand]
    private void SetAiAgent() => ShowUnavailable();

    [RelayCommand]
    private void SetAiAuto() => ShowUnavailable();

    private void ShowUnavailable()
    {
        IsAiOff = true;
        IsAiAgent = false;
        IsAiAuto = false;
        AiStatusText = "IA indisponible";
        AiError = _aiAnalysisClient.UnavailableMessage;
        OnPropertyChanged(nameof(IsAiEnabled));
    }
}
