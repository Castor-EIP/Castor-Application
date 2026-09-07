using Dock.Model.Mvvm.Controls;

namespace CastorApplication.Docking;

// The whole multicam display - grid or spotlight - as one panel, so an operator pulls the
// view out in one piece. Its Context is the page's view-model, and the DataTemplate in
// App.axaml turns that into the display view.
public sealed class MulticamDisplayDocument : Document;

// The AI controls as a panel of their own, so they can be moved, resized or pulled out
// instead of being a fixed strip the operator has to live with.
public sealed class MulticamAiTool : Tool;
