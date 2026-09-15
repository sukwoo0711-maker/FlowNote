using System.Text.Json;
using FlowNote.Core.Models;

namespace FlowNote.Infrastructure.Drafts;

public static class DraftAttachmentCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(IReadOnlyList<PendingAttachment> files)
        => JsonSerializer.Serialize(files, Options);

    public static IReadOnlyList<PendingAttachment> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<PendingAttachment>>(json, Options) ?? [];
    }
}
