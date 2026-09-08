using CastorApplication.Models.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CastorApplication.ViewModels.Settings.Sections;

public partial class StreamingSettingsViewModel : SettingsSectionViewModel
{
    [ObservableProperty]
    private double _streamingBitrate = 4000;

    public string StreamingBitrateDisplay => $"{(int)StreamingBitrate}";

    partial void OnStreamingBitrateChanged(double value)
        => OnPropertyChanged(nameof(StreamingBitrateDisplay));

    protected override void LoadCore(ApplicationSettings settings)
    {
        StreamingBitrate = settings.StreamingBitrate;
    }

    protected override void SaveCore(ApplicationSettings settings)
    {
        settings.StreamingBitrate = StreamingBitrate;
    }
}
