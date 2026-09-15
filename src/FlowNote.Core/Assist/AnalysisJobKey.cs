using FlowNote.Core.Rules;

namespace FlowNote.Core.Assist;

public static class AnalysisJobKey
{
    public static string Compute(
        string entryId,
        string entryRevision,
        int policyRevision,
        string rulesVersion,
        string promptVersion,
        string modelDigest)
        => ContentRevision.Sha256Hex($"{entryId}|{entryRevision}|{policyRevision}|{rulesVersion}|{promptVersion}|{modelDigest}");
}
