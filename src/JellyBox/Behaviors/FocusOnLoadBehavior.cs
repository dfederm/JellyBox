using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace JellyBox.Behaviors;

#pragma warning disable CA1812 // Instantiated via XAML Interaction.Behaviors.
internal sealed class FocusOnLoadBehavior : Behavior<Control>
#pragma warning restore CA1812
{
    protected override void OnAttached()
    {
        AssociatedObject.Loaded += Loaded;
        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= Loaded;
        base.OnDetaching();
    }

    private void Loaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.Focus(FocusState.Programmatic);
    }
}