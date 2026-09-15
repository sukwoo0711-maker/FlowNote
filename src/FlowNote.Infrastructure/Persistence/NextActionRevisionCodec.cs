using System.Text.Json;
using FlowNote.Core.Models;

namespace FlowNote.Infrastructure.Persistence;

internal static class NextActionRevisionCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(
        string requestId,
        NextActionChangeReason reason,
        string? previousText,
        string? previousSourceEntryId,
        string? previousSourceRevision,
        string? newText,
        string? newSourceEntryId,
        string? newSourceRevision)
    {
        return JsonSerializer.Serialize(new Payload
        {
            Kind = "next_action",
            RequestId = requestId,
            Reason = NextActionChangeReasonText.ToStorage(reason),
            PreviousText = previousText,
            PreviousSourceEntryId = previousSourceEntryId,
            PreviousSourceRevision = previousSourceRevision,
            NewText = newText,
            NewSourceEntryId = newSourceEntryId,
            NewSourceRevision = newSourceRevision
        }, Options);
    }

    public static NextActionChange ToChange(
        string id,
        string workItemId,
        DateTimeOffset changedAtUtc,
        string? requestId,
        string json,
        int versionAfter)
    {
        var payload = JsonSerializer.Deserialize<Payload>(json, Options) ?? new Payload();
        return new NextActionChange(
            id,
            workItemId,
            changedAtUtc,
            requestId ?? payload.RequestId ?? "",
            string.IsNullOrEmpty(payload.Reason) ? NextActionChangeReason.Set : NextActionChangeReasonText.Parse(payload.Reason),
            payload.PreviousText,
            payload.PreviousSourceEntryId,
            payload.PreviousSourceRevision,
            payload.NewText,
            payload.NewSourceEntryId,
            payload.NewSourceRevision,
            versionAfter);
    }

    private sealed class Payload
    {
        public string Kind { get; set; } = "next_action";
        public string? RequestId { get; set; }
        public string? Reason { get; set; }
        public string? PreviousText { get; set; }
        public string? PreviousSourceEntryId { get; set; }
        public string? PreviousSourceRevision { get; set; }
        public string? NewText { get; set; }
        public string? NewSourceEntryId { get; set; }
        public string? NewSourceRevision { get; set; }
    }
}
