using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Blurhash;
using CommunityToolkit.Mvvm.ComponentModel;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Kiota.Abstractions.Serialization;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class LazyLoadedImageViewModel : ObservableObject
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;

    [ObservableProperty]
    public partial BaseItemDto? Item { get; set; }

    [ObservableProperty]
    public partial ImageType? ImageType { get; set; }

    [ObservableProperty]
    public partial int Width { get; set; }

    [ObservableProperty]
    public partial int Height { get; set; }

    [ObservableProperty]
    public partial bool EnableBlurHash { get; set; } = true;

    [ObservableProperty]
    public partial Uri? ImageUri { get; set; }

    [ObservableProperty]
    public partial ImageSource? BlurHashImageSource { get; set; }

    public LazyLoadedImageViewModel(JellyfinApiClient jellyfinApiClient)
    {
        _jellyfinApiClient = jellyfinApiClient;
    }

    partial void OnItemChanged(BaseItemDto? value) => InvalidateState();

    partial void OnImageTypeChanged(ImageType? value) => InvalidateState();

    partial void OnWidthChanged(int value) => InvalidateImage();

    partial void OnHeightChanged(int value) => InvalidateImage();

    private void InvalidateState()
    {
        InvalidateBlurHash();
        InvalidateImage();
    }

    private void InvalidateBlurHash()
    {
        if (Item is null
            || ImageType is null
            || !EnableBlurHash)
        {
            return;
        }

        string imageTypeStr = ImageType.Value.ToString();
        if (!Item.ImageTags!.AdditionalData.TryGetValue(imageTypeStr, out object? imageTagObj))
        {
            return;
        }

        string imageTag = imageTagObj.ToString()!;

        // This is a little gross, but there doesn't seem to be a better way to do it.
        IAdditionalDataHolder? blurHashesForType = ImageType.Value switch
        {
            Jellyfin.Sdk.Generated.Models.ImageType.Art => Item.ImageBlurHashes?.Art,
            Jellyfin.Sdk.Generated.Models.ImageType.Backdrop => Item.ImageBlurHashes?.Backdrop,
            Jellyfin.Sdk.Generated.Models.ImageType.Banner => Item.ImageBlurHashes?.Banner,
            Jellyfin.Sdk.Generated.Models.ImageType.Box => Item.ImageBlurHashes?.Box,
            Jellyfin.Sdk.Generated.Models.ImageType.BoxRear => Item.ImageBlurHashes?.BoxRear,
            Jellyfin.Sdk.Generated.Models.ImageType.Chapter => Item.ImageBlurHashes?.Chapter,
            Jellyfin.Sdk.Generated.Models.ImageType.Disc => Item.ImageBlurHashes?.Disc,
            Jellyfin.Sdk.Generated.Models.ImageType.Logo => Item.ImageBlurHashes?.Logo,
            Jellyfin.Sdk.Generated.Models.ImageType.Menu => Item.ImageBlurHashes?.Menu,
            Jellyfin.Sdk.Generated.Models.ImageType.Primary => Item.ImageBlurHashes?.Primary,
            Jellyfin.Sdk.Generated.Models.ImageType.Profile => Item.ImageBlurHashes?.Profile,
            Jellyfin.Sdk.Generated.Models.ImageType.Screenshot => Item.ImageBlurHashes?.Screenshot,
            Jellyfin.Sdk.Generated.Models.ImageType.Thumb => Item.ImageBlurHashes?.Thumb,
            _ => null,
        };
        string? blurHash = null;
        if (blurHashesForType is not null
            && blurHashesForType.AdditionalData.TryGetValue(imageTag, out object? blurHashObj))
        {
            blurHash = blurHashObj.ToString();
        }

        if (blurHash is not null)
        {
            BlurHashImageSource = CreateBlurHashImage(blurHash);
        }
    }

    private void InvalidateImage()
    {
        if (Item is null
            || ImageType is null
            || Width == 0
            || Height == 0)
        {
            return;
        }

        ImageUri = _jellyfinApiClient.GetImageUri(Item, ImageType.Value, Width, Height);
    }

    private static unsafe WriteableBitmap CreateBlurHashImage(string blurhash)
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

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
