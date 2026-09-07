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

    // Set by the AI pipeline through MulticamViewModel.SetAiFocus, never by the tile
    // itself: one scene at a time may be Selected, which only the owner of the
    // whole collection can guarantee.
    [ObservableProperty] private MulticamAiState _aiState = MulticamAiState.None;
    [ObservableProperty] private int _baseCanvasWidth = 1920;
    [ObservableProperty] private int _baseCanvasHeight = 1080;

    /// <summary>Whether this is the scene currently going to the output.</summary>
    public bool IsOnAir => ReferenceEquals(_workspace.ActiveScene, Scene);

    public bool IsAiSelected => AiState == MulticamAiState.Selected;
    public bool IsAiConsidered => AiState == MulticamAiState.Considered;

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

    // Deliberate, explicit action - unlike selecting a scene on the Scenes page, which
    // must never move what is being broadcast.
    [RelayCommand]
    private void PutOnAir() => _workspace.SelectScene(Scene);

    partial void OnAiStateChanged(MulticamAiState value)
    {
        OnPropertyChanged(nameof(IsAiSelected));
        OnPropertyChanged(nameof(IsAiConsidered));
    }

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

    // Kept so a tile rebuilt after a scene change picks its marking back up.
    private SceneItemViewModel? _aiSelectedScene;
    private IReadOnlySet<SceneItemViewModel> _aiConsideredScenes = new HashSet<SceneItemViewModel>();

    public ObservableCollection<SceneItemViewModel> Scenes => _workspace.Scenes;

    /// <summary>The grid itself: one live tile per scene in the workspace.</summary>
    public ObservableCollection<MulticamSceneTile> Tiles { get; } = [];

    public bool HasScenes => Tiles.Count > 0;

    // A multiview fills the page rather than flowing cards: the column count follows
    // the scene count so every tile stays as large as it can be.
    public int GridColumns => ColumnChoice > 0
        ? ColumnChoice
        : Tiles.Count <= 1 ? 1 : Tiles.Count <= 4 ? 2 : Tiles.Count <= 9 ? 3 : 4;

    // 0 means the column count follows the scene count; anything else pins it.
    [ObservableProperty] private int _columnChoice;
    [ObservableProperty] private MulticamLayout _layout = MulticamLayout.Grid;

    public bool IsGridLayout => Layout == MulticamLayout.Grid;
    public bool IsSpotlightLayout => Layout == MulticamLayout.Spotlight;

    // The spotlight renders the scene on air on a surface of its own rather than
    // borrowing a tile: a preview per window is exactly what the runtime supports,
    // and it saves keeping a second, filtered collection of tiles in step.
    public SceneItemViewModel? SpotlightScene => _workspace.ActiveScene;

    public IScenePreviewRuntime PreviewRuntime => _previewRuntime;

    [ObservableProperty] private int _baseCanvasWidth = 1920;
    [ObservableProperty] private int _baseCanvasHeight = 1080;

    public string SpotlightPlaceholderText => !_previewRuntime.IsAvailable
        ? _previewRuntime.UnavailableMessage
        : SpotlightScene == null
            ? "Aucune scène à l'antenne."
            : !StudioWorkspaceViewModel.HasVideoSource(SpotlightScene)
                ? "Pas de source vidéo"
                : "";

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
        (BaseCanvasWidth, BaseCanvasHeight) = CurrentBaseCanvas();
        RefreshTiles();
        Scenes.CollectionChanged += (_, _) => RefreshTiles();
        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
        if (_settingsService != null)
            _settingsService.SettingsSaved += OnSettingsSaved;
    }

    [RelayCommand]
    private void RefreshTiles()
    {
        var (width, height) = CurrentBaseCanvas();

        // Reconciled in place rather than rebuilt: clearing the collection would
        // tear down and restart every native preview on the page each time one
        // scene is added, removed or moved. It also means a tile keeps whatever
        // the operator had set on it - its AI tick above all.
        for (var index = Tiles.Count - 1; index >= 0; index--)
        {
            if (!Scenes.Contains(Tiles[index].Scene)) Tiles.RemoveAt(index);
        }

        for (var index = 0; index < Scenes.Count; index++)
        {
            var scene = Scenes[index];
            var existing = Tiles.FirstOrDefault(tile => tile.Scene == scene);

            if (existing == null)
            {
                Tiles.Insert(index, new MulticamSceneTile(scene, _workspace, _previewRuntime, width, height));
                continue;
            }

            var current = Tiles.IndexOf(existing);
            if (current != index) Tiles.Move(current, index);
        }

        // A scene that has left the workspace can no longer be the AI's pick.
        if (_aiSelectedScene != null && !Scenes.Contains(_aiSelectedScene)) _aiSelectedScene = null;
        ApplyAiFocus();

        OnPropertyChanged(nameof(HasScenes));
        OnPropertyChanged(nameof(GridColumns));
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
        OnPropertyChanged(nameof(SpotlightScene));
        OnPropertyChanged(nameof(SpotlightPlaceholderText));
    }

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        var (width, height) = CurrentBaseCanvas();
        BaseCanvasWidth = width;
        BaseCanvasHeight = height;
        foreach (var tile in Tiles) tile.ApplyBaseCanvas(width, height);
    }

    /// <summary>
    /// Records what the AI is doing with the scenes, as one atomic picture.
    /// Anything not named is cleared, so the grid can never keep showing a
    /// decision the pipeline has moved on from.
    /// </summary>
    /// <param name="selected">The scene the AI would switch to, if any.</param>
    /// <param name="considered">Candidates it is weighing. The selected scene need not repeat here.</param>
    public void SetAiFocus(SceneItemViewModel? selected, IEnumerable<SceneItemViewModel>? considered = null)
    {
        _aiSelectedScene = selected;
        _aiConsideredScenes = considered?.ToHashSet() ?? [];
        ApplyAiFocus();
    }

    // Also runs after the tiles are rebuilt, so a scene added or removed elsewhere
    // does not silently erase what the pipeline last reported.
    private void ApplyAiFocus()
    {
        foreach (var tile in Tiles)
        {
            tile.AiState = ReferenceEquals(tile.Scene, _aiSelectedScene)
                ? MulticamAiState.Selected
                : _aiConsideredScenes.Contains(tile.Scene)
                    ? MulticamAiState.Considered
                    : MulticamAiState.None;
        }
    }

    /// <summary>Drops every AI marking, for when the pipeline stops or is turned off.</summary>
    public void ClearAiFocus() => SetAiFocus(null);

    partial void OnColumnChoiceChanged(int value) => OnPropertyChanged(nameof(GridColumns));

    partial void OnLayoutChanged(MulticamLayout value)
    {
        OnPropertyChanged(nameof(IsGridLayout));
        OnPropertyChanged(nameof(IsSpotlightLayout));
    }

    [RelayCommand]
    private void UseGridLayout() => Layout = MulticamLayout.Grid;

    [RelayCommand]
    private void UseSpotlightLayout() => Layout = MulticamLayout.Spotlight;

    [RelayCommand]
    private void SetColumns(int columns) => ColumnChoice = columns;

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
