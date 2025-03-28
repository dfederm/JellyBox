using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Kiota.Abstractions;

namespace FinBox;

internal static class JellyfinApiClientExtensions
{
    private static readonly Random Random = new();

    public static Uri? GetImageUri(
        this JellyfinApiClient jellyfinApiClient,
        BaseItemDto item,
        ImageType imageType,
        int width,
        int height)
    {
        string imageTypeStr = imageType.ToString();

        // TODO: This is a bit messy. Clean up.
        string? imageTag;
        if (imageType == ImageType.Backdrop)
        {
            if (item.BackdropImageTags is null || item.BackdropImageTags.Count == 0)
            {
                return null;
            }

            imageTag = item.BackdropImageTags[0];
        }
        else
        {
            if (!item.ImageTags!.AdditionalData.TryGetValue(imageTypeStr, out object? imageTagObj))
            {
                return null;
            }

            imageTag = imageTagObj.ToString();
        }

        RequestInformation imageRequest = jellyfinApiClient.Items[item.Id!.Value].Images[imageTypeStr].ToGetRequestInformation(
            request =>
            {
                request.QueryParameters.FillWidth = width;
                request.QueryParameters.FillHeight = height;
                request.QueryParameters.Quality = 96;
                request.QueryParameters.Tag = imageTag;
            });
        return jellyfinApiClient.BuildUri(imageRequest);
    }

    public static Uri? GetItemBackdropImageUrl(this JellyfinApiClient jellyfinApiClient, BaseItemDto item, int? maxWidth)
    {
        RequestInformation? backdropImageRequest = null;

        if (item.Id.HasValue && item.BackdropImageTags?.Count > 0)
        {
            int backdropImgIndex = Random.Next(0, item.BackdropImageTags.Count - 1);
            backdropImageRequest = jellyfinApiClient.Items[item.Id.Value].Images[nameof(ImageType.Backdrop)][backdropImgIndex].ToGetRequestInformation(
                parameters =>
                {
                    parameters.QueryParameters.Tag = item.BackdropImageTags[backdropImgIndex];
                    parameters.QueryParameters.MaxWidth = maxWidth;
                });
        }
        else if (item.ParentBackdropItemId.HasValue && item.ParentBackdropImageTags?.Count > 0)
        {
            int backdropImgIndex = Random.Next(0, item.ParentBackdropImageTags.Count - 1);
            backdropImageRequest = jellyfinApiClient.Items[item.ParentBackdropItemId.Value].Images[nameof(ImageType.Backdrop)][backdropImgIndex].ToGetRequestInformation(
                parameters =>
                {
                    parameters.QueryParameters.Tag = item.ParentBackdropImageTags[backdropImgIndex];
                    parameters.QueryParameters.MaxWidth = maxWidth;
                });
        }
        else if (item.Id.HasValue
            && item.ImageTags?.AdditionalData is not null
            && item.ImageTags.AdditionalData.TryGetValue("Primary", out object? primaryImageTagObj)
            && primaryImageTagObj is string primaryImageTag)
        {
            backdropImageRequest = jellyfinApiClient.Items[item.Id.Value].Images[nameof(ImageType.Primary)].ToGetRequestInformation(
                parameters =>
                {
                    parameters.QueryParameters.Tag = primaryImageTag;
                    parameters.QueryParameters.MaxWidth = maxWidth;
                });
        }

        return backdropImageRequest is not null ? jellyfinApiClient.BuildUri(backdropImageRequest) : null;
    }
}