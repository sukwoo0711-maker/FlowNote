namespace FlowNote.Core.Assist;

public static class AssistPolicy
{
    public static bool CanApply(AssistSettings settings, EntryContextAssignment? current, InferenceResult result, string? errorCode)
    {
        if (settings.Mode == AssistMode.Off)
        {
            return false;
        }

        if (current is { UserLocked: true } || current is { Resolution: AssignmentResolution.ManualClear })
        {
            return false;
        }

        if (errorCode is not null)
        {
            return false;
        }

        if (result.Decision == AssistDecision.Abstain)
        {
            return true;
        }

        if (result.IsFake && settings.Mode == AssistMode.LocalAssist && settings.SemanticAutoApply)
        {
            return false;
        }

        if (settings.Mode == AssistMode.LocalAssist && result.Decision != AssistDecision.Abstain && !settings.SemanticAutoApply && !result.IsFake)
        {
            return result.Decision == AssistDecision.Link;
        }

        return true;
    }
}
