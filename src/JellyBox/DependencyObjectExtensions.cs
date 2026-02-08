using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace JellyBox;

/// <summary>
/// Extension methods for traversing the visual tree.
/// </summary>
internal static class DependencyObjectExtensions
{
    /// <summary>
    /// Finds the first ancestor of the specified type.
    /// </summary>
    public static T? FindAncestor<T>(this DependencyObject element) where T : DependencyObject
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Finds the first descendant of the specified type using depth-first search.
    /// </summary>
    public static T? FindDescendant<T>(this DependencyObject parent) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            T? descendant = child.FindDescendant<T>();
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the first descendant of the specified type matching the predicate using depth-first search.
    /// </summary>
    public static T? FindFirstDescendant<T>(this DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match && predicate(match))
            {
                return match;
            }

            T? descendant = child.FindFirstDescendant(predicate);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds all descendants of the specified type.
    /// </summary>
    public static List<T> FindAllDescendants<T>(this DependencyObject parent) where T : DependencyObject
    {
        List<T> results = [];
        FindAllDescendantsRecursive(parent, results);
        return results;
    }

    private static void FindAllDescendantsRecursive<T>(DependencyObject parent, List<T> results) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                results.Add(match);
            }

            FindAllDescendantsRecursive(child, results);
        }
    }

    /// <summary>
    /// Checks whether the element is a descendant of the specified ancestor in the visual tree.
    /// </summary>
    public static bool IsDescendantOf(this DependencyObject? element, DependencyObject ancestor)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
