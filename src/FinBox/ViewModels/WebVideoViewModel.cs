using CommunityToolkit.Mvvm.ComponentModel;
using FinBox.Views;

namespace FinBox.ViewModels;

internal sealed partial class WebVideoViewModel : ObservableObject
{
    [ObservableProperty]
    public partial Uri VideoUri { get; set; }

    public void HandleParameters(WebVideo.Parameters parameters)
    {
        VideoUri = parameters.VideoUri;
    }
}