using System.Runtime.InteropServices.WindowsRuntime;
using Blurhash;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace JellyBox.Controls;

internal sealed partial class LazyLoadedImage : UserControl
{
    public LazyLoadedImage()
    {
        InitializeComponent();

        ImageFadeIn.Completed += (object? sender, object e) => BlurHashImageSource = null;
    }

    #region ImageUri DependencyProperty

    public Uri? ImageUri
    {
        get => (Uri?)GetValue(ImageUriProperty);
        set => SetValue(ImageUriProperty, value);
    }

    public static readonly DependencyProperty ImageUriProperty = DependencyProperty.Register(
        nameof(ImageUri),
        typeof(Uri),
        typeof(LazyLoadedImage),
        new PropertyMetadata(default(Uri?)));

    #endregion

    #region BlurHash DependencyProperty

    public string? BlurHash
    {
        get => (string?)GetValue(BlurHashProperty);
        set => SetValue(BlurHashProperty, value);
    }

    public static readonly DependencyProperty BlurHashProperty = DependencyProperty.Register(
        nameof(BlurHash),
        typeof(string),
        typeof(LazyLoadedImage),
        new PropertyMetadata(default(string?), OnBlurHashChanged));

    private static void OnBlurHashChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LazyLoadedImage)d;
        string? blurHash = (string?)e.NewValue;

        control.BlurHashImageSource = string.IsNullOrEmpty(blurHash) ? null : CreateBlurHashImage(blurHash);
    }

    #endregion

    #region BlurHashImageSource DependencyProperty

    internal ImageSource? BlurHashImageSource
    {
        get => (ImageSource?)GetValue(BlurHashImageSourceProperty);
        private set => SetValue(BlurHashImageSourceProperty, value);
    }

    private static readonly DependencyProperty BlurHashImageSourceProperty = DependencyProperty.Register(
        nameof(BlurHashImageSource),
        typeof(ImageSource),
        typeof(LazyLoadedImage),
        new PropertyMetadata(default(ImageSource?)));

    #endregion

    private void ImageOpened(object sender, RoutedEventArgs e) => ImageFadeIn.Begin();

    private static WriteableBitmap? CreateBlurHashImage(string blurhash)
    {
        try
        {
            const int width = 20;
            const int height = 20;

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
            var pixelData = new Pixel[width, height];
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
            Core.Decode(blurhash, pixelData, 1);

            WriteableBitmap bitmap = new(width, height);
            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        Pixel pixel = pixelData[row, col];
                        stream.WriteByte((byte)MathUtils.LinearTosRgb(pixel.Blue));
                        stream.WriteByte((byte)MathUtils.LinearTosRgb(pixel.Green));
                        stream.WriteByte((byte)MathUtils.LinearTosRgb(pixel.Red));
                        stream.WriteByte(255);
                    }
                }
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
