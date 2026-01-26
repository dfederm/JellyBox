using JellyBox.Models;
using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml.Controls;

namespace JellyBox.Behaviors;

/// <summary>
/// Invokes the NavigateCommand on INavigable items when they are clicked in a ListViewBase control.
/// </summary>
internal sealed class ListViewBaseCommandBehavior : Behavior<ListViewBase>
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