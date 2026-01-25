using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace JellyBox.Services;

internal readonly record struct JellyfinImage(Uri? Uri, string? BlurHash);

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed class JellyfinImageResolver
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private static readonly Random Random = new();

    private readonly JellyfinApiClient _apiClient;

    public JellyfinImageResolver(JellyfinApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public JellyfinImage ResolveImage(BaseItemDto item, ImageType imageType, int width, int height)
    {
        if (!item.Id.HasValue)
        {
            return default;
        }

        string imageTypeStr = imageType.ToString();
        string? imageTag = GetImageTag(item, imageType, imageTypeStr);
        return imageTag is null
            ? default
            : new JellyfinImage(
                BuildImageUri(item.Id.Value, imageTypeStr, imageTag, width, height),
                GetBlurHash(item.ImageBlurHashes, imageType, imageTag));
    }

    public Uri? GetBackdropImageUri(BaseItemDto item, int? maxWidth)
    {
        RequestInformation? backdropImageRequest = null;

        if (item.Id.HasValue && item.BackdropImageTags?.Count > 0)
        {
            int backdropImgIndex = Random.Next(0, item.BackdropImageTags.Count);
            backdropImageRequest = _apiClient.Items[item.Id.Value].Images[nameof(ImageType.Backdrop)][backdropImgIndex].ToGetRequestInformation(
                parameters =>
                {
                    parameters.QueryParameters.Tag = item.BackdropImageTags[backdropImgIndex];
                    parameters.QueryParameters.MaxWidth = maxWidth;
                });
        }
        else if (item.ParentBackdropItemId.HasValue && item.ParentBackdropImageTags?.Count > 0)
        {
            int backdropImgIndex = Random.Next(0, item.ParentBackdropImageTags.Count);
            backdropImageRequest = _apiClient.Items[item.ParentBackdropItemId.Value].Images[nameof(ImageType.Backdrop)][backdropImgIndex].ToGetRequestInformation(
                parameters =>
                {
                    parameters.QueryParameters.Tag = item.ParentBackdropImageTags[backdropImgIndex];
                    parameters.QueryParameters.MaxWidth = maxWidth;
                });
        }
        else if (item.Id.HasValue
            && item.ImageTags?.AdditionalData is not null
            && item.ImageTags.AdditionalData.TryGetValue(nameof(ImageType.Primary), out object? primaryImageTagObj)
            && primaryImageTagObj is string primaryImageTag)
        {
            backdropImageRequest = _apiClient.Items[item.Id.Value].Images[nameof(ImageType.Primary)].ToGetRequestInformation(
                parameters =>
                {
                    parameters.QueryParameters.Tag = primaryImageTag;
                    parameters.QueryParameters.MaxWidth = maxWidth;
                });
        }

        return backdropImageRequest is not null ? _apiClient.BuildUri(backdropImageRequest) : null;
    }

    private static string? GetImageTag(BaseItemDto item, ImageType imageType, string imageTypeStr)
    {
        if (imageType == ImageType.Backdrop)
        {
            if (item.BackdropImageTags is null || item.BackdropImageTags.Count == 0)
            {
                return null;
            }

            return item.BackdropImageTags[0];
        }
        else
        {
            if (item.ImageTags?.AdditionalData is null
                || !item.ImageTags.AdditionalData.TryGetValue(imageTypeStr, out object? imageTagObj))
            {
                return null;
            }

            return imageTagObj.ToString();
        }
    }

    private Uri BuildImageUri(Guid itemId, string imageTypeStr, string imageTag, int width, int height)
    {
        RequestInformation imageRequest = _apiClient.Items[itemId].Images[imageTypeStr].ToGetRequestInformation(
            request =>
            {
                request.QueryParameters.FillWidth = width;
                request.QueryParameters.FillHeight = height;
                request.QueryParameters.Quality = 96;
                request.QueryParameters.Tag = imageTag;
            });
        return _apiClient.BuildUri(imageRequest);
    }

    private static string? GetBlurHash(BaseItemDto_ImageBlurHashes? blurHashes, ImageType imageType, string imageTag)
    {
        if (blurHashes is null)
        {
            return null;
        }

        // This is a little gross, but there doesn't seem to be a better way to do it.
        IAdditionalDataHolder? blurHashesForType = imageType switch
        {
            ImageType.Art => blurHashes.Art,
            ImageType.Backdrop => blurHashes.Backdrop,
            ImageType.Banner => blurHashes.Banner,
            ImageType.Box => blurHashes.Box,
            ImageType.BoxRear => blurHashes.BoxRear,
            ImageType.Chapter => blurHashes.Chapter,
            ImageType.Disc => blurHashes.Disc,
            ImageType.Logo => blurHashes.Logo,
            ImageType.Menu => blurHashes.Menu,
            ImageType.Primary => blurHashes.Primary,
            ImageType.Profile => blurHashes.Profile,
            ImageType.Screenshot => blurHashes.Screenshot,
            ImageType.Thumb => blurHashes.Thumb,
            _ => null,
        };

        if (blurHashesForType is not null
            && blurHashesForType.AdditionalData.TryGetValue(imageTag, out object? blurHashObj))
        {
            return blurHashObj.ToString();
        }

        return null;
    }
}
