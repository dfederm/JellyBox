using JellyBox.Views;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.Services;

internal static class CollectionNavigation
{
    internal static bool TryCreateLibraryParameters(BaseItemDto item, out Library.Parameters parameters)
    {
        parameters = default!;

        if (item.Type != BaseItemDto_Type.CollectionFolder
            || !item.Id.HasValue
            || !TryMapCollectionType(item.CollectionType, out BaseItemKind itemKind))
        {
            return false;
        }

        parameters = new Library.Parameters(
            item.Id.Value,
            itemKind,
            item.Name ?? GetDefaultTitle(item.CollectionType));

        return true;
    }

    private static bool TryMapCollectionType(BaseItemDto_CollectionType? collectionType, out BaseItemKind itemKind)
    {
        itemKind = default;

        switch (collectionType)
        {
            case BaseItemDto_CollectionType.Movies:
                itemKind = BaseItemKind.Movie;
                return true;
            case BaseItemDto_CollectionType.Tvshows:
                itemKind = BaseItemKind.Series;
                return true;
            case BaseItemDto_CollectionType.Boxsets:
                itemKind = BaseItemKind.BoxSet;
                return true;
            case BaseItemDto_CollectionType.Books:
                itemKind = BaseItemKind.Book;
                return true;
            default:
                return false;
        }
    }

    private static string GetDefaultTitle(BaseItemDto_CollectionType? collectionType) => collectionType switch
    {
        BaseItemDto_CollectionType.Movies => "Movies",
        BaseItemDto_CollectionType.Tvshows => "TV Shows",
        BaseItemDto_CollectionType.Boxsets => "Collections",
        BaseItemDto_CollectionType.Books => "Books",
        _ => "Library",
    };
}