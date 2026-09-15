namespace FlowNote.Core.Assist;

public sealed class ContextThread
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleSourceEntryId { get; init; }
    public string? TitleSourceRevision { get; init; }
    public required TitleOrigin TitleOrigin { get; init; }
    public string? WorkItemId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required int Version { get; init; }
    public string? IssueKey { get; init; }
}

public sealed class EntryContextAssignment
{
    public required string EntryId { get; init; }
    public required string SourceRevision { get; init; }
    public string? ThreadId { get; init; }
    public required AssignmentOrigin Origin { get; init; }
    public required AssignmentResolution Resolution { get; init; }
    public required ContextRole Role { get; init; }
    public required AssignmentOrigin RoleOrigin { get; init; }
    public string? SourceQuote { get; init; }
    public string? AnalysisRunId { get; init; }
    public required bool UserLocked { get; init; }
    public required int CorrectionRevision { get; init; }
}

public sealed class ContextMention
{
    public required string Id { get; init; }
    public required string EntryId { get; init; }
    public required string ThreadId { get; init; }
    public required ContextRole Role { get; init; }
    public required string SourceQuote { get; init; }
    public required string SourceRevision { get; init; }
    public required AssignmentOrigin Origin { get; init; }
    public string? NextActionQuote { get; init; }
}

public sealed class ActionCandidate
{
    public required string Id { get; init; }
    public string? ThreadId { get; init; }
    public required string EntryId { get; init; }
    public required string SourceRevision { get; init; }
    public required string SourceQuote { get; init; }
    public required ActionCandidateKind Kind { get; init; }
    public required ActionCandidateState State { get; init; }
    public string? LinkedWorkItemId { get; init; }
    public DateTimeOffset? AcceptedByUserAtUtc { get; init; }
    public required int Version { get; init; }
}

public sealed class AnalysisJob
{
    public required string Id { get; init; }
    public required string JobKey { get; init; }
    public required string EntryId { get; init; }
    public required string EntryRevision { get; init; }
    public required int PolicyRevision { get; init; }
    public required string RulesVersion { get; init; }
    public required string PromptVersion { get; init; }
    public required string ModelDigest { get; init; }
    public required AnalysisJobStatus Status { get; init; }
    public required int Attempts { get; init; }
    public DateTimeOffset? LeaseUntilUtc { get; init; }
    public DateTimeOffset? NextAttemptUtc { get; init; }
    public string? ErrorCode { get; init; }
    public required int CorrectionSnapshot { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class ThreadCandidate
{
    public required string ThreadId { get; init; }
    public required string Title { get; init; }
    public IReadOnlyList<string> Excerpts { get; init; } = [];
    public string? IssueKey { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
}

public sealed class InferenceRequest
{
    public required string EntryId { get; init; }
    public required string NoteText { get; init; }
    public IReadOnlyList<string> AttachmentNames { get; init; } = [];
    public IReadOnlyList<ThreadCandidate> Candidates { get; init; } = [];
    public required string EntryRevision { get; init; }
    public required int CorrectionRevision { get; init; }
    public required int PolicyRevision { get; init; }

    public string? ContinuationThreadId { get; init; }
}

public sealed class InferencePrimary
{
    public string? ThreadId { get; init; }
    public string? TopicQuote { get; init; }
    public required ContextRole Role { get; init; }
    public required string SourceQuote { get; init; }
    public string? NextActionQuote { get; init; }
}

public sealed class InferenceResult
{
    public required string EntryId { get; init; }
    public required AssistDecision Decision { get; init; }
    public InferencePrimary? Primary { get; init; }
    public IReadOnlyList<InferencePrimary> Mentions { get; init; } = [];
    public string? ErrorCode { get; init; }
    public bool IsFake { get; init; }
}

public sealed class AssistSettings
{
    public AssistMode Mode { get; init; } = AssistMode.RulesOnly;
    public int PolicyRevision { get; init; } = 1;
    public bool SemanticAutoApply { get; init; }
    public string OllamaBaseUrl { get; init; } = AssistVersions.DefaultOllamaBaseUrl;
    public string ModelTag { get; init; } = AssistVersions.ModelTag;
    public string? LockedDigest { get; init; }
}

public sealed class DayFlowEpisode
{
    public required string ThreadId { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<string> ObservedEntryIds { get; init; }
    public bool HasObservationGap { get; init; }
}

public sealed class DayFlowRequestMarker
{
    public required string EntryId { get; init; }
    public required string ThreadId { get; init; }
    public required string Title { get; init; }
    public required ContextRole Role { get; init; }
}

public sealed class DayFlowProjection
{
    public required IReadOnlyList<DayFlowEpisode> Episodes { get; init; }
    public required IReadOnlyList<DayFlowRequestMarker> RequestMarkers { get; init; }
    public required IReadOnlyList<string> UnclassifiedEntryIds { get; init; }
    public string Legend { get; init; } = "기록 기반 연결 · 실작업시간 아님";
}

public interface IContextInference
{
    bool IsFake { get; }

    bool IsAvailable { get; }

    Task<InferenceResult> InferAsync(InferenceRequest request, CancellationToken cancellationToken);
}
