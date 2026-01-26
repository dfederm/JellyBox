using CommunityToolkit.Mvvm.Input;
using JellyBox.Services;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.Models;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed class CardFactory
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinImageResolver _imageResolver;
    private readonly NavigationManager _navigationManager;

    public CardFactory(JellyfinImageResolver imageResolver, NavigationManager navigationManager)
    {
        _imageResolver = imageResolver;
        _navigationManager = navigationManager;
    }

    public Card CreateFromItem(
        BaseItemDto item,
        CardShape shape,
        ImageType? preferredImageType)
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
        JellyfinImage image = _imageResolver.ResolveImage(item, imageType, imageWidth, imageHeight);

        return new Card
        {
            Name = item.Name!,
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            Image = image,
            NavigateCommand = new RelayCommand(() => _navigationManager.NavigateToItem(item)),
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
