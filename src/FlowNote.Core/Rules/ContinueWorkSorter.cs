namespace FlowNote.Core.Rules;

public static class ContinueWorkSorter
{
    public static IReadOnlyList<T> Sort<T>(
        IEnumerable<T> openItems,
        Func<T, bool> hasNextAction,
        Func<T, DateTimeOffset> contentChangedAt,
        Func<T, string> id)
    {
        return openItems
            .OrderByDescending(hasNextAction)
            .ThenByDescending(contentChangedAt)
            .ThenBy(id, StringComparer.Ordinal)
            .ToList();
    }
}
