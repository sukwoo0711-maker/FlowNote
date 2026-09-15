using System.Text.Json;
using FlowNote.Core.Assist;

namespace FlowNote.Infrastructure.Assist;

internal static class InferenceResultMapper
{
    public static InferenceResult Map(JsonElement root, InferenceRequest request, bool isFake)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return RulesEngine.Abstain(request.EntryId, "invalid-json");
        }

        var entryId = root.GetProperty("entry_id").GetString() ?? request.EntryId;
        var decision = AssistCodec.ParseDecision(root.GetProperty("decision").GetString());
        var primaryEl = root.GetProperty("primary");
        var mentionsEl = root.GetProperty("mentions");
        InferencePrimary? primary = primaryEl.ValueKind == JsonValueKind.Null ? null : MapPrimary(primaryEl);
        var mentions = new List<InferencePrimary>();
        if (mentionsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in mentionsEl.EnumerateArray())
            {
                mentions.Add(MapPrimary(item));
            }
        }

        return new InferenceResult
        {
            EntryId = entryId,
            Decision = decision,
            Primary = primary,
            Mentions = mentions,
            IsFake = isFake
        };
    }

    private static InferencePrimary MapPrimary(JsonElement element)
        => new()
        {
            ThreadId = element.TryGetProperty("thread_id", out var thread) && thread.ValueKind != JsonValueKind.Null
                ? thread.GetString()
                : null,
            TopicQuote = element.TryGetProperty("topic_quote", out var topic) && topic.ValueKind != JsonValueKind.Null
                ? topic.GetString()
                : null,
            Role = AssistCodec.ParseRole(element.GetProperty("role").GetString()),
            SourceQuote = element.GetProperty("source_quote").GetString() ?? "",
            NextActionQuote = element.TryGetProperty("next_action_quote", out var next) && next.ValueKind != JsonValueKind.Null
                ? next.GetString()
                : null
        };
}
