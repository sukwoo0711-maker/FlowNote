namespace FlowNote.Core.Rules;

public static class RecentPreviewSelector
{
    public static IReadOnlyList<T> TakeLatestThenChronological<T>(
        IEnumerable<T> todayItems,
        Func<T, DateTimeOffset> occurred,
        Func<T, long> seq,
        int count = CapsuleLayout.PreviewRowCount)
    {
        return todayItems
            .OrderByDescending(occurred)
            .ThenByDescending(seq)
            .Take(count)
            .OrderBy(occurred)
            .ThenBy(seq)
            .ToList();
    }

    public static bool IsBoardEntry(string kind)
        => kind is "note" or "task_completed";

    public static IReadOnlyList<T> TakeLatestNewestFirst<T>(
        IEnumerable<T> items,
        Func<T, DateTimeOffset> recorded,
        Func<T, long> seq,
        int count = CapsuleLayout.PreviewRowCount)
    {
        return items
            .OrderByDescending(recorded)
            .ThenByDescending(seq)
            .Take(count)
            .ToList();
    }
}
