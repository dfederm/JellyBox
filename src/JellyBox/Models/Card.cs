using CommunityToolkit.Mvvm.ComponentModel;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.Models;

internal enum CardShape
{
    Portrait,
    Backdrop,
    Square,
    Banner,
}

internal sealed partial class Card : ObservableObject
{
    [ObservableProperty]
    public partial BaseItemDto Item { get; set; }

    [ObservableProperty]
    public partial CardShape Shape { get; set; }

    [ObservableProperty]
    public partial ImageType? PreferredImageType { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial ImageType ImageType { get; set; }

    [ObservableProperty]
    public partial int ImageWidth { get; set; }

    [ObservableProperty]
    public partial int ImageHeight { get; set; }

    partial void OnItemChanged(BaseItemDto value) => InvalidateState();

    partial void OnShapeChanged(CardShape value) => InvalidateState();

    partial void OnPreferredImageTypeChanged(ImageType? value) => InvalidateState();

    private void InvalidateState()
    {
        if (Item is null)
        {
            return;
        }

        Name = Item.Name!;

        double aspectRatio = GetAspectRatio(Shape);
        ImageType imageType;
        int imageWidth = Shape == CardShape.Portrait ? 200 : 300;

        // TODO: A bunch of logic is missing here
        if (PreferredImageType == ImageType.Thumb
            && (Item.ImageTags?.AdditionalData.ContainsKey(nameof(ImageType.Thumb)) ?? false))
        {
            imageType = ImageType.Thumb;
        }
        else if (PreferredImageType == ImageType.Thumb
            && Item.BackdropImageTags?.Count > 0)
        {
            imageType = ImageType.Backdrop;
        }
        else if (Item.ImageTags?.AdditionalData.ContainsKey(nameof(ImageType.Primary)) ?? false
            && (Item.Type != BaseItemDto_Type.Episode || Item.ChildCount != 0))
        {
            imageType = ImageType.Primary;

            if (Item.PrimaryImageAspectRatio.HasValue)
            {
                aspectRatio = Item.PrimaryImageAspectRatio.Value;
            }
        }
        else if (Item.BackdropImageTags?.Count > 0)
        {
            imageType = ImageType.Backdrop;
        }
        else
        {
            imageType = ImageType.Primary;
        }

        int imageHeight = (int)Math.Round(imageWidth / aspectRatio);

        ImageType = imageType;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
    }

    private static double GetAspectRatio(CardShape shape)
        => shape switch
        {
            CardShape.Portrait => 2d / 3d,
            CardShape.Backdrop => 16d / 9d,
            CardShape.Square => 1d,
            CardShape.Banner => 1000d / 185d,
            _ => throw new NotImplementedException(),
        };
}
