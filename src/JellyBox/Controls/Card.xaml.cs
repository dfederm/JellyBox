using JellyBox.ViewModels;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace JellyBox.Controls;

internal sealed partial class Card : UserControl
{
    public Card()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<CardViewModel>();
    }

    internal CardViewModel ViewModel { get; }

    public BaseItemDto Item
    {
        get => (BaseItemDto)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item),
        typeof(BaseItemDto),
        typeof(Card),
        new PropertyMetadata(default(BaseItemDto), OnItemChanged));

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Card)d).ViewModel.Item = (BaseItemDto)e.NewValue;

    public CardShape Shape
    {
        get => (CardShape)GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(
        nameof(Shape),
        typeof(CardShape),
        typeof(Card),
        new PropertyMetadata(default(CardShape), OnShapeChanged));

    private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Card)d).ViewModel.Shape = (CardShape)e.NewValue;

    public ImageType? PreferredImageType
    {
        get => (ImageType?)GetValue(PreferredImageTypeProperty);
        set => SetValue(PreferredImageTypeProperty, value);
    }

    public static readonly DependencyProperty PreferredImageTypeProperty = DependencyProperty.Register(
        nameof(PreferredImageType),
        typeof(ImageType?),
        typeof(Card),
        new PropertyMetadata(default(ImageType?), OnPreferredImageTypeChanged));

    private static void OnPreferredImageTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Card)d).ViewModel.PreferredImageType = (ImageType?)e.NewValue;
}
