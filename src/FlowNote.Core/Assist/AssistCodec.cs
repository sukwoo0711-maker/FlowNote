namespace FlowNote.Core.Assist;

public static class AssistCodec
{
    public static string Mode(AssistMode mode) => mode switch
    {
        AssistMode.Off => "off",
        AssistMode.LocalAssist => "local_assist",
        _ => "rules_only"
    };

    public static AssistMode ParseMode(string? value) => value switch
    {
        "off" => AssistMode.Off,
        "local_assist" => AssistMode.LocalAssist,
        _ => AssistMode.RulesOnly
    };

    public static string Role(ContextRole role) => role switch
    {
        ContextRole.Performed => "PERFORMED",
        ContextRole.RequestLater => "REQUEST_LATER",
        ContextRole.RequestUnknown => "REQUEST_UNKNOWN",
        ContextRole.Plan => "PLAN",
        ContextRole.Reference => "REFERENCE",
        ContextRole.CompletionMention => "COMPLETION_MENTION",
        _ => "UNKNOWN"
    };

    public static ContextRole ParseRole(string? value) => value switch
    {
        "PERFORMED" => ContextRole.Performed,
        "REQUEST_LATER" => ContextRole.RequestLater,
        "REQUEST_UNKNOWN" => ContextRole.RequestUnknown,
        "PLAN" => ContextRole.Plan,
        "REFERENCE" => ContextRole.Reference,
        "COMPLETION_MENTION" => ContextRole.CompletionMention,
        _ => ContextRole.Unknown
    };

    public static string Origin(AssignmentOrigin origin) => origin switch
    {
        AssignmentOrigin.User => "user",
        AssignmentOrigin.Model => "model",
        _ => "rule"
    };

    public static AssignmentOrigin ParseOrigin(string? value) => value switch
    {
        "user" => AssignmentOrigin.User,
        "model" => AssignmentOrigin.Model,
        _ => AssignmentOrigin.Rule
    };

    public static string Resolution(AssignmentResolution resolution) => resolution switch
    {
        AssignmentResolution.Assigned => "assigned",
        AssignmentResolution.ManualClear => "manual_clear",
        _ => "abstained"
    };

    public static AssignmentResolution ParseResolution(string? value) => value switch
    {
        "assigned" => AssignmentResolution.Assigned,
        "manual_clear" => AssignmentResolution.ManualClear,
        _ => AssignmentResolution.Abstained
    };

    public static string Decision(AssistDecision decision) => decision switch
    {
        AssistDecision.Link => "link",
        AssistDecision.New => "new",
        _ => "abstain"
    };

    public static AssistDecision ParseDecision(string? value) => value switch
    {
        "link" => AssistDecision.Link,
        "new" => AssistDecision.New,
        _ => AssistDecision.Abstain
    };

    public static string JobStatus(AnalysisJobStatus status) => status switch
    {
        AnalysisJobStatus.Running => "running",
        AnalysisJobStatus.Succeeded => "succeeded",
        AnalysisJobStatus.Abstained => "abstained",
        AnalysisJobStatus.RetryWait => "retry_wait",
        AnalysisJobStatus.Blocked => "blocked",
        AnalysisJobStatus.Stale => "stale",
        AnalysisJobStatus.Failed => "failed",
        _ => "pending"
    };

    public static AnalysisJobStatus ParseJobStatus(string? value) => value switch
    {
        "running" => AnalysisJobStatus.Running,
        "succeeded" => AnalysisJobStatus.Succeeded,
        "abstained" => AnalysisJobStatus.Abstained,
        "retry_wait" => AnalysisJobStatus.RetryWait,
        "blocked" => AnalysisJobStatus.Blocked,
        "stale" => AnalysisJobStatus.Stale,
        "failed" => AnalysisJobStatus.Failed,
        _ => AnalysisJobStatus.Pending
    };

    public static string FormatTitleOrigin(TitleOrigin origin) => origin == TitleOrigin.User ? "user" : "derived";

    public static TitleOrigin ParseTitleOrigin(string? value) => value == "user" ? TitleOrigin.User : TitleOrigin.Derived;

    public static string CandidateKind(ActionCandidateKind kind) => kind switch
    {
        ActionCandidateKind.NextAction => "next_action",
        ActionCandidateKind.CompletionMention => "completion_mention",
        _ => "request"
    };

    public static ActionCandidateKind ParseCandidateKind(string? value) => value switch
    {
        "next_action" => ActionCandidateKind.NextAction,
        "completion_mention" => ActionCandidateKind.CompletionMention,
        _ => ActionCandidateKind.Request
    };

    public static string CandidateState(ActionCandidateState state) => state switch
    {
        ActionCandidateState.Accepted => "accepted",
        ActionCandidateState.Dismissed => "dismissed",
        ActionCandidateState.Superseded => "superseded",
        ActionCandidateState.Stale => "stale",
        _ => "suggested"
    };

    public static ActionCandidateState ParseCandidateState(string? value) => value switch
    {
        "accepted" => ActionCandidateState.Accepted,
        "dismissed" => ActionCandidateState.Dismissed,
        "superseded" => ActionCandidateState.Superseded,
        "stale" => ActionCandidateState.Stale,
        _ => ActionCandidateState.Suggested
    };

    public static string RoleLabel(ContextRole role) => role switch
    {
        ContextRole.Performed => "수행",
        ContextRole.RequestLater => "나중에 요청",
        ContextRole.RequestUnknown => "요청",
        ContextRole.Plan => "계획",
        ContextRole.Reference => "참조",
        ContextRole.CompletionMention => "완료 언급",
        _ => "미분류"
    };

    public static string OriginLabel(AssignmentOrigin origin) => origin switch
    {
        AssignmentOrigin.User => "사용자",
        AssignmentOrigin.Model => "로컬 모델",
        _ => "규칙"
    };
}
