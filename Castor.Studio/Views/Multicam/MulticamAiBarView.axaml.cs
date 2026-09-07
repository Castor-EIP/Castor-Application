using Avalonia.Controls;

namespace CastorApplication.Views.Multicam;

// The AI controls, separated from the page so the docking layer can hold them as a panel of
// their own - moved, resized or pulled out like any other.
public partial class MulticamAiBarView : UserControl
{
    public MulticamAiBarView()
    {
        InitializeComponent();
    }
}
