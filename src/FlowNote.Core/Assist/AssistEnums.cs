namespace FlowNote.Core.Assist;

public enum AssistMode
{
    Off,
    RulesOnly,
    LocalAssist
}

public enum ContextRole
{
    Performed,
    RequestLater,
    RequestUnknown,
    Plan,
    Reference,
    CompletionMention,
    Unknown
}

public enum AssignmentOrigin
{
    User,
    Rule,
    Model
}

public enum AssignmentResolution
{
    Assigned,
    Abstained,
    ManualClear
}

public enum AssistDecision
{
    Link,
    New,
    Abstain
}

public enum ActionCandidateKind
{
    Request,
    NextAction,
    CompletionMention
}

public enum ActionCandidateState
{
    Suggested,
    Accepted,
    Dismissed,
    Superseded,
    Stale
}

public enum AnalysisJobStatus
{
    Pending,
    Running,
    Succeeded,
    Abstained,
    RetryWait,
    Blocked,
    Stale,
    Failed
}

public enum TitleOrigin
{
    User,
    Derived
}

public static class AssistVersions
{
    public const string RulesVersion = "v4-r2";
    public const string PolicyVersion = "v4-p1";
    public const string ModelTag = "Qwen3-4B-Q4_K_M";
    public const string RulesDigestToken = "rules";
    public const string DefaultOllamaBaseUrl = "http://127.0.0.1:11435";
    public const string EmbeddedProvider = "embedded-llama.cpp";
    public const string EngineReleaseTag = "b10964";
    public const string ModelFileName = "Qwen3-4B-Q4_K_M.gguf";
    public const int IdleStopSeconds = 120;
    public const int ColdStartupSeconds = 180;
    public const int ConnectTimeoutSeconds = 2;
    public const int StopDrainSeconds = 3;
    public const int PortAttempts = 3;
    public const int JsonMaxDepth = 32;
    public const int MaxCandidates = 6;
    public const int MaxMentions = 2;
    public const int TopicQuoteMax = 60;
    public const int SourceQuoteMax = 240;
    public const int NextActionQuoteMax = 200;
    public const int GapAnnotationMinutes = 30;
    public const int LeaseSeconds = 120;
    public const int HttpTimeoutSeconds = 90;
    public const int MaxAttempts = 3;
    public const int CircuitBreakAfter = 3;
    public const int CircuitBreakSeconds = 60;
    public const int MaxResponseBytes = 65536;
    public const int MaxInputUtf8Bytes = 6500;
}
