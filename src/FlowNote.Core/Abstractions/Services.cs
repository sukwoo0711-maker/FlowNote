using FlowNote.Core.Models;

namespace FlowNote.Core.Abstractions;

public interface IEntryService
{
    Task<TimelineEntry> SaveNoteAsync(SaveNoteRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimelineEntry>> ListForLocalDateAsync(DateOnly localDate, CancellationToken cancellationToken = default);

    Task<TimelineEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<TimelineEntry> RelinkNoteAsync(RelinkNoteRequest request, CancellationToken cancellationToken = default);

    Task<TimelineEntry> UpdateNoteAsync(UpdateNoteRequest request, CancellationToken cancellationToken = default);

    Task<TimelineEntry> SoftDeleteNoteAsync(string entryId, CancellationToken cancellationToken = default);
}

public interface IWorkItemService
{
    Task<WorkItemMutationResult> CreateAsync(CreateWorkItemRequest request, CancellationToken cancellationToken = default);

    Task<WorkItemMutationResult> CompleteAsync(WorkItemCommandRequest request, CancellationToken cancellationToken = default);

    Task<WorkItemMutationResult> ReopenAsync(WorkItemCommandRequest request, CancellationToken cancellationToken = default);

    Task<WorkItemMutationResult> CancelAsync(WorkItemCommandRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> ListByStatusAsync(WorkItemStatus status, CancellationToken cancellationToken = default);

    Task<WorkItem?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<WorkItemMutationResult> SetNextActionAsync(NextActionRequest request, CancellationToken cancellationToken = default);
}
