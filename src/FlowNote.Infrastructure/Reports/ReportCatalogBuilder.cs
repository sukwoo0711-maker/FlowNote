using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Infrastructure.Reports;

public sealed class ReportCatalogBuilder
{
    private readonly FlowNoteDatabase _database;

    public ReportCatalogBuilder(FlowNoteDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<ReportCandidate> ForDate(DateOnly reportDate, string? workItemFilter, IReadOnlyList<string> extraEntryIds)
    {
        var range = _database.DisplayTimeZone.GetUtcRange(reportDate);
        var lifecycle = _database.Entries.ListLifecycleThrough(range.ExclusiveEndUtc);
        var day = _database.Entries.ListForLocalDateAsync(reportDate).GetAwaiter().GetResult();
        var extras = new List<TimelineEntry>();
        foreach (var id in extraEntryIds)
        {
            var entry = _database.Entries.GetByIdAsync(id).GetAwaiter().GetResult();
            if (entry is not null && entry.DeletedAtUtc is null && day.All(item => item.Id != entry.Id))
            {
                extras.Add(entry);
            }
        }

        var extraIds = extras.Select(static item => item.Id).ToHashSet(StringComparer.Ordinal);
        var catalog = new List<ReportCandidate>();
        foreach (var entry in day.Concat(extras))
        {
            if (workItemFilter is not null && entry.WorkItemId != workItemFilter)
            {
                continue;
            }

            catalog.Add(ToCandidate(entry, reportDate, lifecycle, extraIds.Contains(entry.Id)));
        }

        if (workItemFilter is null)
        {
            AddNextActionCandidates(catalog, reportDate, range.ExclusiveEndUtc, lifecycle);
        }
        else
        {
            AddNextActionForWork(catalog, workItemFilter, reportDate, range.ExclusiveEndUtc);
        }

        return catalog;
    }

    private void AddNextActionCandidates(
        List<ReportCandidate> catalog,
        DateOnly reportDate,
        DateTimeOffset exclusiveEnd,
        IReadOnlyList<TimelineEntry> lifecycle)
    {
        var workIds = catalog.Select(static item => item.WorkItemId).Where(static id => id is not null).Distinct();
        foreach (var workId in workIds)
        {
            AddNextActionForWork(catalog, workId!, reportDate, exclusiveEnd);
        }

        _ = lifecycle;
    }

    private void AddNextActionForWork(List<ReportCandidate> catalog, string workItemId, DateOnly reportDate, DateTimeOffset exclusiveEnd)
    {
        var asOf = _database.WorkItems.GetNextActionAsOf(workItemId, exclusiveEnd);
        if (!asOf.Recorded || string.IsNullOrWhiteSpace(asOf.Text))
        {
            return;
        }

        var item = _database.WorkItems.GetAsync(workItemId).GetAwaiter().GetResult();
        catalog.Add(new ReportCandidate
        {
            Id = "next-action:" + workItemId + ":" + reportDate.ToString("yyyyMMdd"),
            Kind = ReportCandidateKind.NextAction,
            OccurredLocalDate = reportDate,
            OccurredAtUtc = exclusiveEnd.AddTicks(-1),
            Seq = long.MaxValue,
            Body = asOf.Text,
            Title = "다음 행동",
            WorkItemId = workItemId,
            WorkTitle = item?.Title,
            IssueKey = item?.IssueKey,
            WorkStatusAsOf = item?.Status,
            BodyRevision = ContentRevision.Sha256Hex(asOf.Text),
            OutOfRange = false
        });
    }

    private ReportCandidate ToCandidate(
        TimelineEntry entry,
        DateOnly reportDate,
        IReadOnlyList<TimelineEntry> lifecycle,
        bool extra)
    {
        var localDate = _database.DisplayTimeZone.GetLocalDate(entry.OccurredAtUtc);
        WorkItem? work = null;
        if (entry.WorkItemId is not null)
        {
            work = _database.WorkItems.GetAsync(entry.WorkItemId).GetAwaiter().GetResult();
        }

        var files = _database.Entries.ListAttachments(entry.Id)
            .Select(item => new ReportFileCandidate
            {
                AttachmentId = item.Id,
                EntryId = entry.Id,
                OriginalName = item.OriginalName,
                Sha256 = item.Sha256,
                ByteSize = item.ByteSize,
                MediaType = item.MediaType
            })
            .ToList();

        var kind = entry.Kind switch
        {
            EntryKind.Note => ReportCandidateKind.Note,
            EntryKind.TaskCompleted => ReportCandidateKind.LifecycleCompleted,
            _ => ReportCandidateKind.LifecycleOther
        };

        return new ReportCandidate
        {
            Id = entry.Id,
            Kind = kind,
            OccurredLocalDate = localDate,
            OccurredAtUtc = entry.OccurredAtUtc,
            Seq = entry.Seq,
            Body = string.IsNullOrWhiteSpace(entry.Body) ? (entry.TitleSnapshot ?? "") : entry.Body,
            Title = entry.TitleSnapshot,
            WorkItemId = entry.WorkItemId,
            WorkTitle = work?.Title ?? entry.TitleSnapshot,
            IssueKey = entry.IssueKey ?? work?.IssueKey,
            WorkStatusAsOf = entry.WorkItemId is null ? null : WorkAsOf.StatusAsOf(lifecycle, entry.WorkItemId),
            BodyRevision = ContentRevision.Sha256Hex(entry.Body + "|" + (entry.UpdatedAtUtc?.ToString("O") ?? "")),
            OutOfRange = extra || localDate != reportDate,
            Files = files,
            AssistProvenance = entry.Kind == EntryKind.Note ? _database.Assist.Provenance(entry.Id) : null
        };
    }
}
