using System.Windows.Input;

namespace JellyBox.Models;

/// <summary>
/// Interface for UI models that support navigation when activated.
/// </summary>
internal interface INavigable
{
    ICommand NavigateCommand { get; }
}
