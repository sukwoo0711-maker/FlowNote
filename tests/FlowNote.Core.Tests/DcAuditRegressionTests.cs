using FlowNote.Core.Assist;

namespace FlowNote.Core.Tests;

public sealed class DcAuditRegressionTests
{
    [Fact]
    public void Deferred_marker_does_not_mark_same_work_return()
    {
        var marks = DayFlowReadModel.MarkSameWorkReturns([(true, "A"), (false, "B"), (true, "A")]);
        Assert.Equal([false, false, false], marks);
    }

    [Fact]
    public void Request_for_a_work_does_not_count_as_earlier_performance()
    {
        var marks = DayFlowReadModel.MarkSameWorkReturns([(false, "B"), (true, "A"), (true, "B")]);
        Assert.Equal([false, false, false], marks);
    }

    [Fact]
    public void Actual_work_return_is_preserved()
    {
        var marks = DayFlowReadModel.MarkSameWorkReturns([(true, "A"), (false, "C"), (true, "B"), (true, "A")]);
        Assert.Equal([false, false, false, true], marks);
    }

    [Fact]
    public void First_deferred_request_in_mixed_note_is_not_lost()
    {
        var request = Request("인버터 확인 중. 새 코스표는 나중에 보기");
        var result = new RulesEngine().Decide(request, false, false);
        Assert.Equal(AssistDecision.Link, result.Decision);
        Assert.Equal("A", result.Primary?.ThreadId);
        var mention = Assert.Single(result.Mentions);
        Assert.Null(mention.ThreadId);
        Assert.NotNull(mention.TopicQuote);
        Assert.Equal(ContextRole.RequestLater, mention.Role);
        Assert.Null(InferenceValidator.Validate(request, result));
    }

    [Theory]
    [InlineData("인버터 검토를 완료했다고 할 수 없다")]
    [InlineData("인버터를 끝냈다고 생각했지만 아직 실패 중")]
    public void Negated_completion_is_not_a_completion_boundary(string note)
    {
        Assert.NotEqual(ContextRole.CompletionMention, AssistText.ClassifyRole(note));
    }

    [Theory]
    [InlineData("인버터 확인 중 코스표 요청은 나중에")]
    [InlineData("나중에 재현해보자")]
    public void Single_mixed_clause_never_recurses(string text)
    {
        var result = new RulesEngine().Decide(Request(text), false, false);
        Assert.Equal(AssistDecision.Abstain, result.Decision);
        Assert.Equal("mixed-clause-ambiguous", result.ErrorCode);
    }

    [Theory]
    [InlineData(",")]
    [InlineData(";")]
    [InlineData("\n")]
    public void Bounded_clause_separators_keep_deferred_request(string separator)
    {
        var request = Request("인버터 확인 중" + separator + "새 코스표는 나중에 보기");
        var result = new RulesEngine().Decide(request, false, false);
        Assert.Equal("A", result.Primary?.ThreadId);
        Assert.Single(result.Mentions);
        Assert.Null(InferenceValidator.Validate(request, result));
    }

    private static InferenceRequest Request(string text) => new()
    {
        EntryId = "entry", NoteText = text, EntryRevision = "r", PolicyRevision = 1,
        CorrectionRevision = 0,
        Candidates = [new ThreadCandidate { ThreadId = "A", Title = "인버터 과전류", Aliases = ["인버터"] }]
    };
}
