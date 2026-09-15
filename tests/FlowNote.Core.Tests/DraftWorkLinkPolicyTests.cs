using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Core.Tests;

public sealed class DraftWorkLinkPolicyTests
{
    [Fact]
    public void Empty_draft_applies_directly()
    {
        Assert.Equal(DraftWorkLinkDecision.ApplyDirectly, DraftWorkLinkPolicy.Decide(null, "A", hasOwnedDraft: false));
    }

    [Fact]
    public void Same_work_needs_no_dialog()
    {
        Assert.Equal(DraftWorkLinkDecision.SameWork, DraftWorkLinkPolicy.Decide("A", "A", hasOwnedDraft: true));
    }

    [Fact]
    public void Owned_draft_to_other_work_needs_confirm()
    {
        Assert.Equal(DraftWorkLinkDecision.Confirm, DraftWorkLinkPolicy.Decide("A", "B", hasOwnedDraft: true));
    }
}
