using FlowNote.Core.Rules;

namespace FlowNote.Core.Tests;

public sealed class RecentPreviewSelectorTests
{
    [Fact]
    public void Picks_latest_three_then_shows_them_chronologically()
    {
        var items = new (DateTimeOffset At, long Seq, string Id)[]
        {
            (new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero), 1, "a"),
            (new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero), 2, "b"),
            (new DateTimeOffset(2026, 9, 14, 11, 0, 0, TimeSpan.Zero), 3, "c"),
            (new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero), 4, "d"),
            (new DateTimeOffset(2026, 9, 14, 13, 0, 0, TimeSpan.Zero), 5, "e")
        };

        var picked = RecentPreviewSelector.TakeLatestThenChronological(items, static x => x.At, static x => x.Seq);
        Assert.Equal(new[] { "c", "d", "e" }, picked.Select(static x => x.Id).ToArray());
    }

    [Fact]
    public void Empty_today_returns_empty()
    {
        var picked = RecentPreviewSelector.TakeLatestThenChronological(
            Array.Empty<(DateTimeOffset At, long Seq)>(),
            static x => x.At,
            static x => x.Seq);
        Assert.Empty(picked);
    }

    [Fact]
    public void Newest_first_puts_just_saved_on_top()
    {
        var items = new (DateTimeOffset At, long Seq, string Id)[]
        {
            (new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero), 1, "old"),
            (new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero), 2, "mid"),
            (new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero), 3, "new")
        };

        var picked = RecentPreviewSelector.TakeLatestNewestFirst(items, static x => x.At, static x => x.Seq);
        Assert.Equal(new[] { "new", "mid", "old" }, picked.Select(static x => x.Id).ToArray());
    }

    [Fact]
    public void Board_keeps_notes_and_completions_only()
    {
        Assert.True(RecentPreviewSelector.IsBoardEntry("note"));
        Assert.True(RecentPreviewSelector.IsBoardEntry("task_completed"));
        Assert.False(RecentPreviewSelector.IsBoardEntry("task_created"));
        Assert.False(RecentPreviewSelector.IsBoardEntry("task_reopened"));
    }
}
