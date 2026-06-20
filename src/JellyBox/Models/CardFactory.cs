using System.Globalization;
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

            // TODO: Is this necessary?
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
            Name = GetDisplayName(item),
            SecondaryText = GetSecondaryText(item),
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            Image = image,
            NavigateCommand = new RelayCommand(() => _navigationManager.NavigateToItem(item)),
            IsFavorite = item.UserData?.IsFavorite ?? false,
            IsPlayed = item.UserData?.Played ?? false,
            PlayedPercentage = item.UserData?.PlayedPercentage ?? 0,
            UnplayedItemCount = item.UserData?.UnplayedItemCount ?? 0,
        };
    }

    public Card CreateFromPerson(
        BaseItemPerson person,
        CardShape shape)
    {
        double aspectRatio = GetAspectRatio(shape);
        int imageWidth = shape == CardShape.Portrait ? 200 : 300;
        int imageHeight = (int)Math.Round(imageWidth / aspectRatio);
        JellyfinImage image = _imageResolver.ResolveImage(person, imageWidth, imageHeight);

        return new Card
        {
            Name = person.Name!,
            SecondaryText = person.Role,
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            Image = image,
            NavigateCommand = new RelayCommand(() => _navigationManager.NavigateToPerson(person)),
        };
    }

    private static string GetDisplayName(BaseItemDto item)
        => item.Type switch
        {
            BaseItemDto_Type.Episode => item.SeriesName ?? item.Name!,
            _ => item.Name!,
        };

    private static string? GetSecondaryText(BaseItemDto item)
        => item.Type switch
        {
            BaseItemDto_Type.Episode => GetEpisodeText(item),
            BaseItemDto_Type.Season => item.SeriesName,
            BaseItemDto_Type.MusicAlbum => item.AlbumArtist,
            BaseItemDto_Type.Audio => item.AlbumArtist ?? item.Album,
            BaseItemDto_Type.MusicVideo => item.AlbumArtist ?? item.Album,
            BaseItemDto_Type.Series => GetSeriesYearText(item),
            BaseItemDto_Type.Movie => item.ProductionYear?.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };

    private static string? GetSeriesYearText(BaseItemDto item)
    {
        if (item.ProductionYear is null)
        {
            return null;
        }

        string startYear = item.ProductionYear.Value.ToString(CultureInfo.InvariantCulture);

        if (string.Equals(item.Status, "Continuing", StringComparison.OrdinalIgnoreCase))
        {
            return $"{startYear} - Present";
        }

        if (item.EndDate.HasValue)
        {
            string endYear = item.EndDate.Value.Year.ToString(CultureInfo.InvariantCulture);
            return endYear == startYear ? startYear : $"{startYear} - {endYear}";
        }

        return startYear;
    }

    private static string GetEpisodeText(BaseItemDto item)
    {
        string prefix = item.ParentIndexNumber.HasValue && item.IndexNumber.HasValue
            ? $"S{item.ParentIndexNumber.Value}:E{item.IndexNumber.Value} - "
            : item.IndexNumber.HasValue
                ? $"E{item.IndexNumber.Value} - "
                : string.Empty;

        return $"{prefix}{item.Name}";
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
