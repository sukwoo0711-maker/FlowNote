namespace FlowNote.Core.Assist;

public static class DayFlowReadModel
{
    public static bool[] MarkSameWorkReturns(IReadOnlyList<(bool IsWorkSegment, string ThreadId)> ordered)
    {
        var marks = new bool[ordered.Count];
        var leftSince = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (var i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            if (string.IsNullOrEmpty(item.ThreadId))
            {
                continue;
            }

            if (item.IsWorkSegment)
            {
                marks[i] = leftSince.GetValueOrDefault(item.ThreadId);
                leftSince[item.ThreadId] = false;
            }

            foreach (var key in leftSince.Keys.ToList())
            {
                if (key != item.ThreadId)
                {
                    leftSince[key] = true;
                }
            }

            leftSince.TryAdd(item.ThreadId, false);
        }

        return marks;
    }
}
