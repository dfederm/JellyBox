using JellyBox.Views;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.Services;

internal static class CollectionNavigation
{
    internal static bool TryCreateLibraryParameters(BaseItemDto item, out Library.Parameters parameters)
    {
        parameters = default!;

        if (item.Type is not BaseItemDto_Type.CollectionFolder
            || !item.Id.HasValue
            || !TryMapCollectionType(item.CollectionType, out BaseItemKind itemKind, out string defaultTitle))
        {
            return false;
        }

        parameters = new Library.Parameters(
            item.Id.Value,
            itemKind,
            item.Name ?? defaultTitle);

        return true;
    }

    private static bool TryMapCollectionType(
        BaseItemDto_CollectionType? collectionType,
        out BaseItemKind itemKind,
        out string defaultTitle)
    {
        switch (collectionType)
        {
            case BaseItemDto_CollectionType.Movies:
                itemKind = BaseItemKind.Movie;
                defaultTitle = "Movies";
                return true;
            case BaseItemDto_CollectionType.Tvshows:
                itemKind = BaseItemKind.Series;
                defaultTitle = "TV Shows";
                return true;
            case BaseItemDto_CollectionType.Boxsets:
                itemKind = BaseItemKind.BoxSet;
                defaultTitle = "Collections";
                return true;
            case BaseItemDto_CollectionType.Books:
                itemKind = BaseItemKind.Book;
                defaultTitle = "Books";
                return true;
            default:
                itemKind = default;
                defaultTitle = "Library";
                return false;
        }
    }
}