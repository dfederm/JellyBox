using JellyBox.Services;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.Models;

internal static class CardFactory
{
    public static Card CreateFromItem(
        BaseItemDto item,
        CardShape shape,
        ImageType? preferredImageType,
        JellyfinImageResolver imageResolver)
    {
        double aspectRatio = GetAspectRatio(shape);
        ImageType imageType;
        int imageWidth = shape == CardShape.Portrait ? 200 : 300;

        // TODO: A bunch of logic is missing here
        if (preferredImageType == ImageType.Thumb
            && (item.ImageTags?.AdditionalData.ContainsKey(nameof(ImageType.Thumb)) ?? false))
        {
            imageType = ImageType.Thumb;
        }
        else if (preferredImageType == ImageType.Thumb
            && item.BackdropImageTags?.Count > 0)
        {
            imageType = ImageType.Backdrop;
        }
        else if (item.ImageTags?.AdditionalData.ContainsKey(nameof(ImageType.Primary)) ?? false
            && (item.Type != BaseItemDto_Type.Episode || item.ChildCount != 0))
        {
            imageType = ImageType.Primary;

            if (item.PrimaryImageAspectRatio.HasValue)
            {
                aspectRatio = item.PrimaryImageAspectRatio.Value;
            }
        }
        else if (item.BackdropImageTags?.Count > 0)
        {
            imageType = ImageType.Backdrop;
        }
        else
        {
            imageType = ImageType.Primary;
        }

        int imageHeight = (int)Math.Round(imageWidth / aspectRatio);
        JellyfinImage image = imageResolver.ResolveImage(item, imageType, imageWidth, imageHeight);

        return new Card
        {
            Item = item,
            Name = item.Name!,
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            Image = image,
        };
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
