namespace FlowNote.Core.Assist;

public static class DayFlowReadModel
{
    public static bool[] MarkSameWorkReturns(IReadOnlyList<(bool IsWorkSegment, string ThreadId)> ordered)
    {
        var marks = new bool[ordered.Count];
        var seenWork = new HashSet<string>(StringComparer.Ordinal);
        string? previousWork = null;
        for (var i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            if (!item.IsWorkSegment || string.IsNullOrEmpty(item.ThreadId))
            {
                continue;
            }

            marks[i] = previousWork != item.ThreadId && seenWork.Contains(item.ThreadId);
            seenWork.Add(item.ThreadId);
            previousWork = item.ThreadId;
        }

        return marks;
    }
}
