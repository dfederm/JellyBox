using JellyBox.Models;
using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml.Controls;

namespace JellyBox.Behaviors;

/// <summary>
/// Invokes the NavigateCommand on INavigable items when they are clicked in a ListViewBase control.
/// </summary>
#pragma warning disable CA1812 // Instantiated via XAML Interaction.Behaviors.
internal sealed class ListViewBaseCommandBehavior : Behavior<ListViewBase>
#pragma warning restore CA1812
{
    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.ItemClick += ItemClicked;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        AssociatedObject.ItemClick -= ItemClicked;
    }

    private static void ItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is INavigable navigable)
        {
            if (navigable.NavigateCommand.CanExecute(null))
            {
                navigable.NavigateCommand.Execute(null);
            }
        }
    }
}