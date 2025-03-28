using System.Windows.Input;
using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FinBox.Behaviors;

/// <summary>
/// Creates an attached property for all ListViewBase controls allowing binding  a command object to it's ItemClick event.
/// </summary>
internal sealed class ListViewBaseCommandBehavior : Behavior<ListViewBase>
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        "Command",
        typeof(ICommand),
        typeof(ListViewBaseCommandBehavior),
        new PropertyMetadata(null));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

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

    private void ItemClicked(object sender, ItemClickEventArgs e)
    {
        ICommand command = Command;
        if (command is not null && command.CanExecute(e.ClickedItem))
        {
            command.Execute(e.ClickedItem);
        }
    }
}