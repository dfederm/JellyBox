using CommunityToolkit.Mvvm.ComponentModel;
using JellyBox.Views;

namespace JellyBox.ViewModels;

internal sealed partial class WebVideoViewModel : ObservableObject
{
    [ObservableProperty]
    public partial Uri VideoUri { get; set; }

    public void HandleParameters(WebVideo.Parameters parameters)
    {
        VideoUri = parameters.VideoUri;
    }
}