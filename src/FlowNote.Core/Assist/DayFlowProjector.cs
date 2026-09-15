using FlowNote.Core.Models;

namespace FlowNote.Core.Assist;

public static class DayFlowProjector
{
    public static DayFlowProjection Project(
        IReadOnlyList<TimelineEntry> entries,
        IReadOnlyDictionary<string, EntryContextAssignment> assignments,
        IReadOnlyDictionary<string, ContextThread> threads)
    {
        var ordered = entries
            .Where(static item => item.DeletedAtUtc is null)
            .OrderBy(static item => item.OccurredAtUtc)
            .ThenBy(static item => item.Seq)
            .ToList();

        var episodes = new List<OpenEpisode>();
        var requests = new List<DayFlowRequestMarker>();
        var unclassified = new List<string>();
        OpenEpisode? open = null;
        var workToThread = threads.Values
            .Where(static item => item.WorkItemId is not null)
            .GroupBy(static item => item.WorkItemId!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        foreach (var entry in ordered)
        {
            if (entry.Kind is EntryKind.TaskCompleted or EntryKind.TaskCancelled &&
                entry.WorkItemId is not null &&
                workToThread.TryGetValue(entry.WorkItemId, out var closedThread) &&
                open is not null &&
                open.ThreadId == closedThread.Id)
            {
                open = null;
                continue;
            }

            if (entry.Kind != EntryKind.Note)
            {
                continue;
            }

            if (!assignments.TryGetValue(entry.Id, out var assignment) ||
                assignment.Resolution != AssignmentResolution.Assigned ||
                assignment.ThreadId is null)
            {
                unclassified.Add(entry.Id);
                continue;
            }

            if (!threads.TryGetValue(assignment.ThreadId, out var thread))
            {
                unclassified.Add(entry.Id);
                continue;
            }

            switch (assignment.Role)
            {
                case ContextRole.RequestLater:
                case ContextRole.RequestUnknown:
                case ContextRole.Plan:
                case ContextRole.Reference:
                    requests.Add(new DayFlowRequestMarker
                    {
                        EntryId = entry.Id,
                        ThreadId = thread.Id,
                        Title = thread.Title,
                        Role = assignment.Role
                    });
                    break;
                case ContextRole.Performed:
                case ContextRole.CompletionMention:
                    if (open is null || open.ThreadId != thread.Id)
                    {
                        open = new OpenEpisode(thread.Id, thread.Title);
                        episodes.Add(open);
                    }

                    open.Observed.Add(entry.Id);
                    open.Times.Add(entry.OccurredAtUtc);
                    if (assignment.Role == ContextRole.CompletionMention)
                    {
                        open = null;
                    }

                    break;
                default:
                    unclassified.Add(entry.Id);
                    break;
            }
        }

        var closed = episodes.Select(item => new DayFlowEpisode
        {
            ThreadId = item.ThreadId,
            Title = item.Title,
            ObservedEntryIds = item.Observed,
            HasObservationGap = HasGap(item.Times)
        }).ToList();

        return new DayFlowProjection
        {
            Episodes = closed,
            RequestMarkers = requests,
            UnclassifiedEntryIds = unclassified
        };
    }

    private static bool HasGap(IReadOnlyList<DateTimeOffset> times)
    {
        for (var i = 1; i < times.Count; i++)
        {
            if ((times[i] - times[i - 1]).TotalMinutes > AssistVersions.GapAnnotationMinutes)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class OpenEpisode(string threadId, string title)
    {
        public string ThreadId { get; } = threadId;
        public string Title { get; } = title;
        public List<string> Observed { get; } = [];
        public List<DateTimeOffset> Times { get; } = [];
    }
}
