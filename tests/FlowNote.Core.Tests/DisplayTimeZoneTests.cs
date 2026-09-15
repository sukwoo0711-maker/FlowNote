using FlowNote.Core.Errors;
using FlowNote.Core.Time;

namespace FlowNote.Core.Tests;

public sealed class DisplayTimeZoneTests
{
    private readonly DisplayTimeZone _korea = DisplayTimeZone.Korea();

    [Fact]
    public void Korea_date_boundary_is_not_utc_date()
    {
        var beforeMidnight = new DateTimeOffset(2026, 9, 14, 14, 59, 0, TimeSpan.Zero);
        var afterMidnight = new DateTimeOffset(2026, 9, 14, 15, 1, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 14), _korea.GetLocalDate(beforeMidnight));
        Assert.Equal(new DateOnly(2026, 9, 15), _korea.GetLocalDate(afterMidnight));

        var sept14 = _korea.GetUtcRange(new DateOnly(2026, 9, 14));
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 15, 0, 0, TimeSpan.Zero), sept14.InclusiveStartUtc);
        Assert.Equal(new DateTimeOffset(2026, 9, 14, 15, 0, 0, TimeSpan.Zero), sept14.ExclusiveEndUtc);
        Assert.True(beforeMidnight >= sept14.InclusiveStartUtc && beforeMidnight < sept14.ExclusiveEndUtc);
        Assert.False(afterMidnight >= sept14.InclusiveStartUtc && afterMidnight < sept14.ExclusiveEndUtc);
    }

    [Fact]
    public void Invalid_dst_time_is_rejected_without_correction()
    {
        var pacific = new DisplayTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        Assert.Throws<InvalidLocalTimeException>(() =>
            pacific.LocalToUtc(new DateOnly(2026, 3, 8), new TimeOnly(2, 30)));
    }

    [Fact]
    public void Ambiguous_dst_time_requires_offset()
    {
        var pacific = new DisplayTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        Assert.Throws<AmbiguousLocalTimeException>(() =>
            pacific.LocalToUtc(new DateOnly(2026, 11, 1), new TimeOnly(1, 30)));

        var chosen = pacific.LocalToUtc(new DateOnly(2026, 11, 1), new TimeOnly(1, 30), TimeSpan.FromHours(-8));
        Assert.Equal(TimeSpan.Zero, chosen.Offset);
    }
}
