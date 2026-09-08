using Dock.Model.Mvvm.Controls;

namespace CastorApplication.Docking;

// The whole multicam display - grid or spotlight - as one panel, so an operator pulls the
// view out in one piece. A tool rather than a document: tools get the chrome bar with its
// collapse and pin controls, which is what makes a panel feel grabbable, and Dock drops the
// system frame for a floating tool window on its own.
public sealed class MulticamDisplayTool : Tool;

// The AI controls as a panel of their own, so they can be moved, resized or pulled out
// instead of being a fixed strip the operator has to live with.
public sealed class MulticamAiTool : Tool;
