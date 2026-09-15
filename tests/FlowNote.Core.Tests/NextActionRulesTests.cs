using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Core.Tests;

public sealed class NextActionRulesTests
{
    [Fact]
    public void Blank_text_is_rejected_except_clear()
    {
        Assert.Throws<ValidationException>(() => NextActionRules.ValidateAndNormalize("   ", NextActionChangeReason.Set));
        Assert.Null(NextActionRules.ValidateAndNormalize(null, NextActionChangeReason.Clear));
    }

    [Fact]
    public void Over_200_chars_is_not_truncated()
    {
        var text = new string('가', 201);
        var error = Assert.Throws<ValidationException>(() => NextActionRules.ValidateAndNormalize(text, NextActionChangeReason.Set));
        Assert.Contains("200", error.Message);
        Assert.Equal(201, text.Length);
    }

    [Fact]
    public void Completed_work_cannot_activate_next_action()
    {
        Assert.Throws<ValidationException>(() => NextActionRules.EnsureCanActivate(WorkItemStatus.Completed, NextActionChangeReason.Set));
        NextActionRules.EnsureCanActivate(WorkItemStatus.Open, NextActionChangeReason.Set);
    }

    [Fact]
    public void As_of_does_not_invent_history()
    {
        var none = NextActionRules.ProjectAsOf([], new DateTimeOffset(2026, 9, 14, 15, 0, 0, TimeSpan.Zero));
        Assert.False(none.Recorded);
        Assert.Null(none.Text);

        var history = new[]
        {
            new NextActionChange("1", "w", new DateTimeOffset(2026, 9, 14, 0, 25, 0, TimeSpan.Zero), "r1",
                NextActionChangeReason.Set, null, null, null, "어제 행동", null, null, 2),
            new NextActionChange("2", "w", new DateTimeOffset(2026, 9, 15, 0, 20, 0, TimeSpan.Zero), "r2",
                NextActionChangeReason.Completion, "어제 행동", null, null, null, null, null, 3)
        };
        var asOf14 = NextActionRules.ProjectAsOf(history, new DateTimeOffset(2026, 9, 14, 15, 0, 0, TimeSpan.Zero));
        Assert.Equal("어제 행동", asOf14.Text);
        var asOf15 = NextActionRules.ProjectAsOf(history, new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero));
        Assert.Null(asOf15.Text);
        Assert.True(asOf15.Recorded);
    }
}
