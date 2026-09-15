using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Core.Tests;

public sealed class TimelineDisplayRulesTests
{
    [Fact]
    public void Same_occurred_and_recorded_time_is_not_repeated()
    {
        var at = DateTimeOffset.Parse("2026-09-14T09:12:00+09:00");
        Assert.Null(TimelineDisplayRules.TimeDifferenceLabel(at, at));
    }

    [Fact]
    public void Different_occurred_and_recorded_times_are_both_shown()
    {
        var occurred = DateTimeOffset.Parse("2026-09-14T09:12:00+09:00");
        var recorded = DateTimeOffset.Parse("2026-09-14T11:03:00+09:00");
        Assert.Equal("발생 09:12 · 기록 11:03", TimelineDisplayRules.TimeDifferenceLabel(occurred, recorded));
    }

    [Fact]
    public void Node_kind_matches_lifecycle_without_using_color_alone()
    {
        Assert.Equal("created", TimelineDisplayRules.NodeKind(EntryKind.TaskCreated));
        Assert.Equal("completed", TimelineDisplayRules.NodeKind(EntryKind.TaskCompleted));
        Assert.Equal("reopened", TimelineDisplayRules.NodeKind(EntryKind.TaskReopened));
        Assert.Equal("할 일 생성", TimelineDisplayRules.KindLabel(EntryKind.TaskCreated));
        Assert.Equal("다시 열림", TimelineDisplayRules.KindLabel(EntryKind.TaskReopened));
    }
}

public sealed class FileSizeDisplayTests
{
    [Fact]
    public void Formats_bytes_and_kilobytes()
    {
        Assert.Equal("12 B", FileSizeDisplay.Format(12));
        Assert.Equal("1.5 KB", FileSizeDisplay.Format(1536));
    }
}
