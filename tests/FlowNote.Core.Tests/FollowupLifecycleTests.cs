using FlowNote.Core.Assist;
using FlowNote.Core.Models;

namespace FlowNote.Core.Tests;

public sealed class FollowupLifecycleTests
{
    [Fact]
    public void Reopening_is_visible_between_completion_and_recompletion()
    {
        var entries = new[]
        {
            Event("done-1", 1, EntryKind.TaskCompleted),
            Event("undo-1", 2, EntryKind.TaskReopened),
            Event("done-2", 3, EntryKind.TaskCompleted)
        };
        var projected = DayFlowProjector.Project(entries,
            new Dictionary<string, EntryContextAssignment>(), new Dictionary<string, ContextThread>());
        Assert.Equal(new[] { "done-1", "undo-1", "done-2" }, projected.OfficialMarkers.Select(marker => marker.EntryId));
        Assert.Equal(EntryKind.TaskReopened, projected.OfficialMarkers[1].Kind);
        Assert.Equal("다시 열림", projected.OfficialMarkers[1].Title);
        Assert.Empty(projected.Episodes);
    }

    private static TimelineEntry Event(string id, long seq, EntryKind kind) => new()
    {
        Id = id, Seq = seq, RequestId = id, Kind = kind, WorkItemId = "w",
        TitleSnapshot = "보드 점검", Body = "",
        RecordedAtUtc = DateTimeOffset.Parse("2026-09-16T00:00:00Z").AddMinutes(seq),
        OccurredAtUtc = DateTimeOffset.Parse("2026-09-16T00:00:00Z").AddMinutes(seq),
        OccurredTimeSource = OccurredTimeSource.Recorded, RecordedOffsetMinutes = 540
    };
}
