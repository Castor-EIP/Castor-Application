using Avalonia.Controls;

namespace CastorApplication.Views.Multicam;

// The multicam display itself - grid or spotlight - separated from the page so it can be
// handed to the dock as a single panel and pulled out whole.
public partial class MulticamDisplayView : UserControl
{
    public MulticamDisplayView()
    {
        InitializeComponent();
    }
}
