namespace JellyBox.ViewModels;

/// <summary>
/// Interface for ViewModels that support loading state indication.
/// </summary>
internal interface ILoadingViewModel
{
    /// <summary>
    /// Gets a value indicating whether the ViewModel is currently loading data.
    /// </summary>
    bool IsLoading { get; }
}
