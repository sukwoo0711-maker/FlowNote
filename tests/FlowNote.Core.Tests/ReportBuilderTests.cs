using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Core.Tests;

public sealed class ReportBuilderTests
{
    [Fact]
    public void Unselected_private_token_is_absent()
    {
        var catalog = new[]
        {
            Candidate("pub", "공개 메모", outOfRange: false),
            Candidate("priv", "PRIVATE-DO-NOT-EXPORT 비밀", outOfRange: false)
        };
        var snapshot = ReportBuilder.Build(new ReportBuildRequest
        {
            ReportDate = new DateOnly(2026, 9, 15),
            TimeZoneDisplay = "Asia/Seoul",
            SelectedIds = ["pub"],
            SelectedAttachmentIds = [],
            Catalog = catalog
        }, DateTimeOffset.UtcNow);

        Assert.Contains("공개 메모", snapshot.Markdown);
        Assert.DoesNotContain("PRIVATE-DO-NOT-EXPORT", snapshot.Markdown);
        Assert.DoesNotContain("priv", snapshot.Markdown);
    }

    [Fact]
    public void Out_of_range_keeps_original_date()
    {
        var catalog = new[]
        {
            Candidate("old", "전날 근거", outOfRange: true, date: new DateOnly(2026, 9, 14))
        };
        var snapshot = ReportBuilder.Build(new ReportBuildRequest
        {
            ReportDate = new DateOnly(2026, 9, 15),
            TimeZoneDisplay = "Asia/Seoul",
            SelectedIds = ["old"],
            SelectedAttachmentIds = [],
            Catalog = catalog
        }, DateTimeOffset.UtcNow);

        Assert.Contains("2026-09-14", snapshot.Markdown);
        Assert.Contains("참고 기록", snapshot.Markdown);
    }

    [Fact]
    public void Empty_selection_cannot_export()
    {
        Assert.Throws<ValidationException>(() => ReportBuilder.Build(new ReportBuildRequest
        {
            ReportDate = new DateOnly(2026, 9, 15),
            TimeZoneDisplay = "Asia/Seoul",
            SelectedIds = [],
            SelectedAttachmentIds = [],
            Catalog = [Candidate("a", "x", false)]
        }, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Stale_when_body_changes()
    {
        var first = Candidate("a", "원문", false);
        var snapshot = ReportBuilder.Build(new ReportBuildRequest
        {
            ReportDate = new DateOnly(2026, 9, 15),
            TimeZoneDisplay = "Asia/Seoul",
            SelectedIds = ["a"],
            SelectedAttachmentIds = [],
            Catalog = [first]
        }, DateTimeOffset.UtcNow);
        var edited = Candidate("a", "바뀐 원문", false);
        Assert.True(ReportBuilder.IsStale(snapshot, [edited]));
    }

    private static ReportCandidate Candidate(string id, string body, bool outOfRange, DateOnly? date = null)
    {
        return new ReportCandidate
        {
            Id = id,
            Kind = ReportCandidateKind.Note,
            OccurredLocalDate = date ?? new DateOnly(2026, 9, 15),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Seq = 1,
            Body = body,
            BodyRevision = ContentRevision.Sha256Hex(body),
            OutOfRange = outOfRange
        };
    }
}
