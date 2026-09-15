using System.Windows;
using System.Windows.Media;

namespace FlowNote.Desktop.Smoke;

internal static class VisualTreeScan
{
    public static List<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        var list = new List<T>();
        Walk(root, list);
        return list;
    }

    private static void Walk<T>(DependencyObject current, List<T> list) where T : DependencyObject
    {
        if (current is T match)
        {
            list.Add(match);
        }

        var count = VisualTreeHelper.GetChildrenCount(current);
        for (var i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(current, i), list);
        }
    }
}
