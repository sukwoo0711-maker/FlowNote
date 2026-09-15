using FlowNote.Core.Models;

namespace FlowNote.Core.Rules;

public static class DraftWorkLinkPolicy
{
    public static bool HasOwnedDraft(string? body, int attachmentCount)
        => !string.IsNullOrWhiteSpace(body) || attachmentCount > 0;

    public static DraftWorkLinkDecision Decide(string? draftWorkItemId, string requestedWorkItemId, bool hasOwnedDraft)
    {
        if (string.Equals(draftWorkItemId, requestedWorkItemId, StringComparison.Ordinal))
        {
            return DraftWorkLinkDecision.SameWork;
        }

        if (!hasOwnedDraft)
        {
            return DraftWorkLinkDecision.ApplyDirectly;
        }

        return DraftWorkLinkDecision.Confirm;
    }
}
