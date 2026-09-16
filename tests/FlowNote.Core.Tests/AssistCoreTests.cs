using FlowNote.Core.Assist;
using FlowNote.Core.Models;

namespace FlowNote.Core.Tests;

public sealed class RulesEngineTests
{
    private readonly RulesEngine _engine = new();

    [Fact]
    public void Unique_issue_key_links_without_assuming_performed()
    {
        var result = _engine.Decide(Request("DEV-42 로그 확인", [Candidate("t1", "DEV-42")]), false, false);
        Assert.Equal(AssistDecision.Link, result.Decision);
        Assert.Equal("t1", result.Primary?.ThreadId);
        Assert.Equal(ContextRole.Unknown, result.Primary?.Role);
        Assert.Equal("DEV-42", result.Primary?.SourceQuote);
    }

    [Fact]
    public void Multiple_issue_keys_abstain()
    {
        var result = _engine.Decide(
            Request("DEV-1 그리고 ABC-2", [Candidate("t1", "DEV-1"), Candidate("t2", "ABC-2")]),
            false,
            false);
        Assert.Equal(AssistDecision.Abstain, result.Decision);
        Assert.Equal("multiple-issue-keys", result.ErrorCode);
    }

    [Fact]
    public void Unique_alias_links()
    {
        var result = _engine.Decide(
            Request("게이트 드라이버 순서 확인", [new ThreadCandidate
            {
                ThreadId = "t-alias",
                Title = "기존 묶음",
                Aliases = ["게이트 드라이버"]
            }]),
            false,
            false);
        Assert.Equal(AssistDecision.Link, result.Decision);
        Assert.Equal("t-alias", result.Primary?.ThreadId);
        Assert.Equal(ContextRole.Unknown, result.Primary?.Role);
    }

    [Fact]
    public void User_lock_wins()
    {
        var result = _engine.Decide(Request("DEV-42", [Candidate("t1", "DEV-42")]), userLocked: true, manualClear: false);
        Assert.Equal(AssistDecision.Abstain, result.Decision);
        Assert.Equal("user-lock", result.ErrorCode);
    }

    [Fact]
    public void Manual_clear_does_not_relink()
    {
        var result = _engine.Decide(Request("DEV-42", [Candidate("t1", "DEV-42")]), false, true);
        Assert.Equal(AssistDecision.Abstain, result.Decision);
    }

    [Fact]
    public void Same_key_two_threads_abstain()
    {
        var result = _engine.Decide(
            Request("DEV-9", [Candidate("a", "DEV-9"), Candidate("b", "DEV-9")]),
            false,
            false);
        Assert.Equal(AssistDecision.Abstain, result.Decision);
    }

    [Fact]
    public void First_note_opens_thread_from_topic()
    {
        var result = _engine.Decide(Request("인버터 과전류 확인 중", []), false, false);
        Assert.Equal(AssistDecision.New, result.Decision);
        Assert.Equal(ContextRole.Performed, result.Primary?.Role);
        Assert.Contains("인버터", result.Primary?.TopicQuote, StringComparison.Ordinal);
    }

    [Fact]
    public void Deferred_request_is_not_performed()
    {
        var result = _engine.Decide(Request("코스표 검토 요청 들어옴. 나중에 보기", []), false, false);
        Assert.Equal(AssistDecision.New, result.Decision);
        Assert.Equal(ContextRole.RequestLater, result.Primary?.Role);
    }

    [Fact]
    public void New_topic_does_not_stick_to_unrelated_continuation()
    {
        var result = _engine.Decide(
            Request(
                "인버터 과전류 확인 중",
                [new ThreadCandidate { ThreadId = "t-board", Title = "보드 전원", Aliases = ["보드", "전원"] }],
                "t-board"),
            false,
            false);
        Assert.Equal(AssistDecision.New, result.Decision);
        Assert.Contains("인버터", result.Primary?.TopicQuote, StringComparison.Ordinal);
    }

    [Fact]
    public void Continuation_keeps_same_thread_without_label()
    {
        var result = _engine.Decide(
            Request("초기화 순서 확인", [new ThreadCandidate { ThreadId = "t-a", Title = "인버터 과전류" }], "t-a"),
            false,
            false);
        Assert.Equal(AssistDecision.Link, result.Decision);
        Assert.Equal("t-a", result.Primary?.ThreadId);
        Assert.Equal(ContextRole.Unknown, result.Primary?.Role);
    }

    [Fact]
    public void Token_match_returns_to_earlier_thread()
    {
        var result = _engine.Decide(
            Request(
                "인버터 조건 하나 더 확인",
                [
                    new ThreadCandidate { ThreadId = "t-a", Title = "인버터 과전류", Aliases = ["인버터"] },
                    new ThreadCandidate { ThreadId = "t-b", Title = "코스표 검토", Aliases = ["코스표"] }
                ]),
            false,
            false);
        Assert.Equal(AssistDecision.Link, result.Decision);
        Assert.Equal("t-a", result.Primary?.ThreadId);
        Assert.Equal(ContextRole.Unknown, result.Primary?.Role);
    }

    [Fact]
    public void Not_reproduced_is_not_completion()
    {
        var result = _engine.Decide(
            Request("순서 변경 후 재현 안 됨", [new ThreadCandidate { ThreadId = "t-a", Title = "인버터 과전류" }], "t-a"),
            false,
            false);
        Assert.Equal(AssistDecision.Link, result.Decision);
        Assert.Equal(ContextRole.Performed, result.Primary?.Role);
        Assert.NotEqual(ContextRole.CompletionMention, result.Primary?.Role);
    }

    [Fact]
    public void Short_note_stays_unlinked()
    {
        var result = _engine.Decide(Request("확인", []), false, false);
        Assert.Equal(AssistDecision.Abstain, result.Decision);
    }

    [Fact]
    public void Generic_note_does_not_stick_to_continuation()
    {
        var result = _engine.Decide(
            Request("확인", [new ThreadCandidate { ThreadId = "t-a", Title = "인버터 과전류" }], "t-a"),
            false,
            false);
        Assert.Equal(AssistDecision.Abstain, result.Decision);
    }

    [Fact]
    public void Plan_is_not_performed()
    {
        var result = _engine.Decide(Request("내일 보드 전원 점검 예정", []), false, false);
        Assert.Equal(AssistDecision.New, result.Decision);
        Assert.Equal(ContextRole.Plan, result.Primary?.Role);
    }

    [Fact]
    public void Request_without_later_is_not_performed()
    {
        var result = _engine.Decide(Request("코스표 검토 요청 들어옴", []), false, false);
        Assert.Equal(AssistDecision.New, result.Decision);
        Assert.Equal(ContextRole.RequestLater, result.Primary?.Role);
        Assert.NotEqual(ContextRole.Performed, result.Primary?.Role);
    }

    [Fact]
    public void Mixed_perform_and_defer_keeps_secondary_mention()
    {
        var result = _engine.Decide(
            Request(
                "인버터 확인 중. 코스표는 나중에 보기",
                [
                    new ThreadCandidate { ThreadId = "t-a", Title = "인버터 과전류", Aliases = ["인버터"] },
                    new ThreadCandidate { ThreadId = "t-b", Title = "코스표 검토", Aliases = ["코스표"] }
                ]),
            false,
            false);
        Assert.Equal(AssistDecision.Link, result.Decision);
        Assert.Equal("t-a", result.Primary?.ThreadId);
        Assert.Equal(ContextRole.Performed, result.Primary?.Role);
        var mention = Assert.Single(result.Mentions);
        Assert.Equal("t-b", mention.ThreadId);
        Assert.Equal(ContextRole.RequestLater, mention.Role);
    }

    [Fact]
    public void Lack_of_later_does_not_confirm_perform()
    {
        var result = _engine.Decide(Request("DEV-42 관련 로그", [Candidate("t1", "DEV-42")]), false, false);
        Assert.Equal(AssistDecision.Link, result.Decision);
        Assert.Equal(ContextRole.Unknown, result.Primary?.Role);
    }

    private static InferenceRequest Request(string note, IReadOnlyList<ThreadCandidate> candidates, string? continuation = null)
        => new()
        {
            EntryId = "e1",
            NoteText = note,
            Candidates = candidates,
            EntryRevision = "r",
            CorrectionRevision = 0,
            PolicyRevision = 1,
            ContinuationThreadId = continuation
        };

    private static ThreadCandidate Candidate(string id, string issue)
        => new() { ThreadId = id, Title = issue, IssueKey = issue };
}

public sealed class InferenceValidatorTests
{
    [Fact]
    public void Invented_quote_is_rejected()
    {
        var request = BaseRequest("원문만 있다");
        var result = new InferenceResult
        {
            EntryId = "e1",
            Decision = AssistDecision.New,
            Primary = new InferencePrimary
            {
                TopicQuote = "없는문구",
                Role = ContextRole.Performed,
                SourceQuote = "없는문구"
            }
        };
        Assert.Equal("source-quote-invalid", InferenceValidator.Validate(request, result));
    }

    [Fact]
    public void Link_unknown_thread_is_rejected()
    {
        var request = BaseRequest("DEV-1 확인", [new ThreadCandidate { ThreadId = "known", Title = "k", IssueKey = "DEV-1" }]);
        var result = new InferenceResult
        {
            EntryId = "e1",
            Decision = AssistDecision.Link,
            Primary = new InferencePrimary
            {
                ThreadId = "missing",
                Role = ContextRole.Unknown,
                SourceQuote = "DEV-1"
            }
        };
        Assert.Equal("link-unknown-thread", InferenceValidator.Validate(request, result));
    }

    [Fact]
    public void Abstain_must_be_empty()
    {
        var request = BaseRequest("모호함");
        var result = new InferenceResult
        {
            EntryId = "e1",
            Decision = AssistDecision.Abstain,
            Primary = new InferencePrimary { Role = ContextRole.Unknown, SourceQuote = "모호함" }
        };
        Assert.Equal("abstain-not-empty", InferenceValidator.Validate(request, result));
    }

    private static InferenceRequest BaseRequest(string note, IReadOnlyList<ThreadCandidate>? candidates = null)
        => new()
        {
            EntryId = "e1",
            NoteText = note,
            Candidates = candidates ?? [],
            EntryRevision = "r",
            CorrectionRevision = 0,
            PolicyRevision = 1
        };
}

public sealed class AssistPolicyTests
{
    [Fact]
    public void Locked_assignment_is_not_overwritten()
    {
        var allowed = AssistPolicy.CanApply(
            new AssistSettings { Mode = AssistMode.RulesOnly },
            new EntryContextAssignment
            {
                EntryId = "e",
                SourceRevision = "r",
                Origin = AssignmentOrigin.User,
                Resolution = AssignmentResolution.Assigned,
                Role = ContextRole.Unknown,
                RoleOrigin = AssignmentOrigin.User,
                UserLocked = true,
                CorrectionRevision = 1
            },
            new InferenceResult { EntryId = "e", Decision = AssistDecision.Link, Primary = new InferencePrimary { ThreadId = "t", Role = ContextRole.Unknown, SourceQuote = "x" } },
            null);
        Assert.False(allowed);
    }

    [Fact]
    public void Rules_only_can_create_new_thread()
    {
        var allowed = AssistPolicy.CanApply(
            new AssistSettings { Mode = AssistMode.RulesOnly },
            null,
            new InferenceResult
            {
                EntryId = "e",
                Decision = AssistDecision.New,
                Primary = new InferencePrimary { TopicQuote = "구체주제명", Role = ContextRole.Performed, SourceQuote = "구체주제명" }
            },
            null);
        Assert.True(allowed);
    }

    [Fact]
    public void Fake_result_cannot_enable_semantic_auto_apply()
    {
        var allowed = AssistPolicy.CanApply(
            new AssistSettings { Mode = AssistMode.LocalAssist, SemanticAutoApply = true },
            null,
            new InferenceResult { EntryId = "e", Decision = AssistDecision.New, IsFake = true, Primary = new InferencePrimary { TopicQuote = "주제", Role = ContextRole.Performed, SourceQuote = "주제" } },
            null);
        Assert.False(allowed);
    }
}

public sealed class DayFlowProjectorTests
{
    [Fact]
    public void Deferred_request_does_not_split_performed_thread()
    {
        var entries = Notes(
            ("n1", 9, 0),
            ("n2", 9, 20),
            ("n3", 9, 45),
            ("n4", 10, 10),
            ("n5", 10, 15));
        var threads = Threads(("thread-a", "A"), ("thread-b", "B"));
        var assignments = Assign(
            ("n1", "thread-a", ContextRole.Performed),
            ("n2", "thread-b", ContextRole.RequestLater),
            ("n3", "thread-a", ContextRole.Performed),
            ("n4", "thread-a", ContextRole.CompletionMention),
            ("n5", "thread-b", ContextRole.Performed));

        var projection = DayFlowProjector.Project(entries, assignments, threads);

        Assert.Equal(2, projection.Episodes.Count);
        Assert.Equal(["n1", "n3", "n4"], projection.Episodes[0].ObservedEntryIds);
        Assert.Equal("thread-a", projection.Episodes[0].ThreadId);
        Assert.True(projection.Episodes[0].HasObservationGap);
        Assert.Equal(["n5"], projection.Episodes[1].ObservedEntryIds);
        Assert.Equal("thread-b", projection.Episodes[1].ThreadId);
        Assert.Equal("n2", Assert.Single(projection.RequestMarkers).EntryId);
        Assert.Equal("기록 기반 연결 · 실작업시간 아님", projection.Legend);
    }

    [Fact]
    public void Actual_switch_splits_episodes_but_keeps_identity()
    {
        var entries = Notes(("s1", 9, 0), ("s2", 9, 30), ("s3", 10, 0));
        var threads = Threads(("ctx-inverter", "A"), ("ctx-course", "B"));
        var assignments = Assign(
            ("s1", "ctx-inverter", ContextRole.Performed),
            ("s2", "ctx-course", ContextRole.Performed),
            ("s3", "ctx-inverter", ContextRole.Performed));

        var projection = DayFlowProjector.Project(entries, assignments, threads);
        Assert.Equal(3, projection.Episodes.Count);
        Assert.Equal("ctx-inverter", projection.Episodes[0].ThreadId);
        Assert.Equal("ctx-course", projection.Episodes[1].ThreadId);
        Assert.Equal("ctx-inverter", projection.Episodes[2].ThreadId);
        Assert.Equal(projection.Episodes[0].ThreadId, projection.Episodes[2].ThreadId);
        Assert.Equal(["s1"], projection.Episodes[0].ObservedEntryIds);
        Assert.Equal(["s3"], projection.Episodes[2].ObservedEntryIds);
    }

    [Fact]
    public void Assigned_unknown_stays_in_the_same_thread_episode()
    {
        var entries = Notes(("a", 9, 0), ("u", 9, 10), ("a2", 9, 20));
        var threads = Threads(("t-a", "A"));
        var assignments = Assign(
            ("a", "t-a", ContextRole.Performed),
            ("u", "t-a", ContextRole.Unknown),
            ("a2", "t-a", ContextRole.Performed));
        var projection = DayFlowProjector.Project(entries, assignments, threads);
        Assert.DoesNotContain("u", projection.UnclassifiedEntryIds);
        Assert.Equal(["a", "u", "a2"], projection.Episodes[0].ObservedEntryIds);
    }

    [Fact]
    public void Official_complete_is_kept_separate_from_observation()
    {
        var notes = Notes(("a", 9, 0));
        var complete = new TimelineEntry
        {
            Seq = 2,
            Id = "done",
            RequestId = "done",
            Kind = EntryKind.TaskCompleted,
            Body = "",
            TitleSnapshot = "공식 완료",
            RecordedAtUtc = new DateTimeOffset(2026, 9, 15, 1, 30, 0, TimeSpan.Zero),
            OccurredAtUtc = new DateTimeOffset(2026, 9, 15, 1, 30, 0, TimeSpan.Zero),
            OccurredTimeSource = OccurredTimeSource.User,
            RecordedOffsetMinutes = 540,
            WorkItemId = "w1"
        };
        var threads = new Dictionary<string, ContextThread>(StringComparer.Ordinal)
        {
            ["t-a"] = new ContextThread
            {
                Id = "t-a",
                Title = "A",
                TitleOrigin = TitleOrigin.Derived,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Version = 1,
                WorkItemId = "w1"
            }
        };
        var assignments = Assign(("a", "t-a", ContextRole.Performed));
        var projection = DayFlowProjector.Project([notes[0], complete], assignments, threads);
        Assert.Equal(["a"], projection.Episodes[0].ObservedEntryIds);
        Assert.Equal("done", Assert.Single(projection.OfficialMarkers).EntryId);
        Assert.Equal("공식 완료", projection.OfficialMarkers[0].Title);
    }

    [Fact]
    public void Secondary_mention_does_not_double_count_original()
    {
        var entries = Notes(("mix", 9, 0));
        var threads = Threads(("t-a", "A"), ("t-b", "B"));
        var assignments = Assign(("mix", "t-a", ContextRole.Performed));
        var mentions = new Dictionary<string, IReadOnlyList<ContextMention>>(StringComparer.Ordinal)
        {
            ["mix"] =
            [
                new ContextMention
                {
                    Id = "m1",
                    EntryId = "mix",
                    ThreadId = "t-b",
                    Role = ContextRole.RequestLater,
                    SourceQuote = "코스표",
                    SourceRevision = "r",
                    Origin = AssignmentOrigin.Rule
                }
            ]
        };
        var projection = DayFlowProjector.Project(entries, assignments, threads, mentions);
        Assert.Equal(["mix"], projection.Episodes[0].ObservedEntryIds);
        Assert.Equal("mix", Assert.Single(projection.RequestMarkers).EntryId);
        Assert.Equal("t-b", projection.RequestMarkers[0].ThreadId);
    }

    [Fact]
    public void Return_mark_requires_intervening_other_work()
    {
        var marks = DayFlowReadModel.MarkSameWorkReturns(
        [
            (true, "a"),
            (true, "b"),
            (true, "a"),
            (true, "a")
        ]);
        Assert.False(marks[0]);
        Assert.True(marks[2]);
        Assert.False(marks[3]);
    }

    private static IReadOnlyList<TimelineEntry> Notes(params (string Id, int Hour, int Minute)[] items)
    {
        var seq = 1L;
        return items.Select(item => new TimelineEntry
        {
            Seq = seq++,
            Id = item.Id,
            RequestId = item.Id,
            Kind = EntryKind.Note,
            Body = item.Id,
            RecordedAtUtc = new DateTimeOffset(2026, 9, 15, item.Hour - 9, item.Minute, 0, TimeSpan.Zero),
            OccurredAtUtc = new DateTimeOffset(2026, 9, 15, item.Hour - 9, item.Minute, 0, TimeSpan.Zero),
            OccurredTimeSource = OccurredTimeSource.User,
            RecordedOffsetMinutes = 540
        }).ToList();
    }

    private static IReadOnlyDictionary<string, ContextThread> Threads(params (string Id, string Title)[] items)
        => items.ToDictionary(
            static item => item.Id,
            static item => new ContextThread
            {
                Id = item.Id,
                Title = item.Title,
                TitleOrigin = TitleOrigin.Derived,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Version = 1
            },
            StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, EntryContextAssignment> Assign(params (string Entry, string ThreadId, ContextRole Role)[] items)
        => items.ToDictionary(
            static item => item.Entry,
            static item => new EntryContextAssignment
            {
                EntryId = item.Entry,
                SourceRevision = "r",
                ThreadId = item.ThreadId,
                Origin = AssignmentOrigin.Rule,
                Resolution = AssignmentResolution.Assigned,
                Role = item.Role,
                RoleOrigin = AssignmentOrigin.Rule,
                UserLocked = false,
                CorrectionRevision = 0
            },
            StringComparer.Ordinal);
}
