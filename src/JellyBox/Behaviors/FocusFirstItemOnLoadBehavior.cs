using System.Collections.Specialized;
using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace JellyBox.Behaviors;

/// <summary>
/// Focuses the first item in a list on the loaded event.
/// </summary>
internal sealed class FocusFirstItemOnLoadBehavior : Behavior<ListViewBase>
{
    // Need to track whether we've attached to the collection changed event
    private bool _collectionChangedSubscribed;

    protected override void OnAttached()
    {
        base.OnAttached();

        // The ItemSource of the listView will not be set yet, 
        // so get a method that we can hook up to later
        AssociatedObject.DataContextChanged += DataContextChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        AssociatedObject.DataContextChanged -= DataContextChanged;

        // Detach from the collection changed event
        if (AssociatedObject.ItemsSource is INotifyCollectionChanged collection && _collectionChangedSubscribed)
        {
            collection.CollectionChanged -= CollectionChanged;
            _collectionChangedSubscribed = false;

        }
    }

    private void DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        // The ObservableCollection implements the INotifyCollectionChanged interface
        // However, if this is bound to something that doesn't then just don't hook the event
        if (AssociatedObject.ItemsSource is INotifyCollectionChanged collection && !_collectionChangedSubscribed)
        {
            // The data context has been changed, so now hook 
            // into the collection changed event
            collection.CollectionChanged += CollectionChanged;
            _collectionChangedSubscribed = true;
        }
    }

    private async void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            // TODO: This happens *every* time an item is added. How can we wait until it's stable?
            DependencyObject firstItem = AssociatedObject.ContainerFromIndex(0);
            if (firstItem is not null)
            {
                await FocusManager.TryFocusAsync(firstItem, FocusState.Programmatic);
            }
        }
        catch (Exception ex)
        {
            // Prevent app crash from async void event handler
            System.Diagnostics.Debug.WriteLine($"Error in CollectionChanged: {ex}");
        }
    }
}
