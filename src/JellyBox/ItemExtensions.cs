using Jellyfin.Sdk.Generated.Models;

namespace JellyBox;

/// <summary>
/// Extension methods for Jellyfin item types.
/// </summary>
internal static class ItemExtensions
{
    /// <summary>
    /// Determines if the item is playable in this app.
    /// </summary>
    public static bool IsPlayable(this BaseItemDto item)
        // Currently we can only play videos.
        => item.MediaType is BaseItemDto_MediaType.Video;

    /// <summary>
    /// Determines if the item can be marked as played/watched.
    /// </summary>
    public static bool CanMarkPlayed(this BaseItemDto item)
    {
        if (item.Type == BaseItemDto_Type.Program)
        {
            return false;
        }

        if (item.MediaType == BaseItemDto_MediaType.Video)
        {
            return item.Type != BaseItemDto_Type.TvChannel;
        }

        if (item.MediaType == BaseItemDto_MediaType.Audio)
        {
            return item.Type is BaseItemDto_Type.AudioBook;
        }

        return item.Type is BaseItemDto_Type.Series
            or BaseItemDto_Type.Season
            or BaseItemDto_Type.BoxSet
            || item.MediaType == BaseItemDto_MediaType.Book;
    }

    /// <summary>
    /// Determines if the item can be favorited.
    /// </summary>
    public static bool CanMarkFavorite(this BaseItemDto item)
        => item.UserData is not null
            && item.Type is not BaseItemDto_Type.Program
            && item.Type is not BaseItemDto_Type.CollectionFolder
            && item.Type is not BaseItemDto_Type.UserView
            && item.Type is not BaseItemDto_Type.Channel;
}
