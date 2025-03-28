using JellyBox.ViewModels;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace JellyBox.Controls;

internal sealed partial class LazyLoadedImage : UserControl
{
    public LazyLoadedImage()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<LazyLoadedImageViewModel>();

        SizeChanged += (object sender, SizeChangedEventArgs e) =>
        {
            ViewModel.Width = (int)e.NewSize.Width;
            ViewModel.Height = (int)e.NewSize.Height;
        };

        ImageFadeIn.Completed += (object? sender, object e) =>
        {
            ViewModel.EnableBlurHash = false;
            ViewModel.BlurHashImageSource = null;
        };
    }

    internal LazyLoadedImageViewModel ViewModel { get; }

    public BaseItemDto Item
    {
        get => (BaseItemDto)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item),
        typeof(BaseItemDto),
        typeof(LazyLoadedImage),
        new PropertyMetadata(default(BaseItemDto), OnItemChanged));

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LazyLoadedImage)d).ViewModel.Item = (BaseItemDto)e.NewValue;

    public ImageType ImageType
    {
        get => (ImageType)GetValue(ImageTypeProperty);
        set => SetValue(ImageTypeProperty, value);
    }

    public static readonly DependencyProperty ImageTypeProperty = DependencyProperty.Register(
        nameof(ImageType),
        typeof(ImageType),
        typeof(LazyLoadedImage),
        new PropertyMetadata(default(ImageType), OnImageTypeChanged));

    private static void OnImageTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LazyLoadedImage)d).ViewModel.ImageType = (ImageType)e.NewValue;

    public bool EnableBlurHash
    {
        get => (bool)GetValue(EnableBlurHashProperty);
        set => SetValue(EnableBlurHashProperty, value);
    }

    public static readonly DependencyProperty EnableBlurHashProperty = DependencyProperty.Register(
        nameof(EnableBlurHash),
        typeof(bool),
        typeof(LazyLoadedImage),
        new PropertyMetadata(defaultValue: true, OnEnableBlurHashChanged));

    private static void OnEnableBlurHashChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LazyLoadedImage)d).ViewModel.EnableBlurHash = (bool)e.NewValue;

    private void ImageOpened(object sender, RoutedEventArgs e) => ImageFadeIn.Begin();
}
