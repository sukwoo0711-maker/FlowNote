using System.Text.Json;
using FlowNote.Core.Assist;

namespace FlowNote.Infrastructure.Assist.Embedded;

public static class OpenAiChatParser
{
    public static InferenceResult Parse(string envelope, InferenceRequest request)
    {
        StrictJson.EnsureNoDuplicateProperties(envelope);
        using var document = JsonDocument.Parse(envelope, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            MaxDepth = AssistVersions.JsonMaxDepth
        });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return RulesEngine.Abstain(request.EntryId, "invalid-json");
        }

        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return RulesEngine.Abstain(request.EntryId, "missing-choices");
        }

        var items = choices.EnumerateArray().ToList();
        if (items.Count != 1)
        {
            return RulesEngine.Abstain(request.EntryId, "choices-count");
        }

        var choice = items[0];
        if (!choice.TryGetProperty("finish_reason", out var finish) || finish.GetString() != "stop")
        {
            return RulesEngine.Abstain(request.EntryId, "finish-reason");
        }

        if (!choice.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return RulesEngine.Abstain(request.EntryId, "missing-content");
        }

        if (message.TryGetProperty("tool_calls", out var tools) && tools.ValueKind is JsonValueKind.Array && tools.GetArrayLength() > 0)
        {
            return RulesEngine.Abstain(request.EntryId, "tool-calls-forbidden");
        }

        if (message.TryGetProperty("function_call", out _))
        {
            return RulesEngine.Abstain(request.EntryId, "tool-calls-forbidden");
        }

        if (message.TryGetProperty("reasoning_content", out var reasoning) &&
            reasoning.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(reasoning.GetString()))
        {
            return RulesEngine.Abstain(request.EntryId, "thinking-output");
        }

        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
        {
            return RulesEngine.Abstain(request.EntryId, "missing-content");
        }

        var inner = content.GetString() ?? "";
        if (inner.Contains("```", StringComparison.Ordinal))
        {
            return RulesEngine.Abstain(request.EntryId, "invalid-json");
        }

        try
        {
            StrictJson.EnsureNoDuplicateProperties(inner);
            using var parsed = JsonDocument.Parse(inner, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
                MaxDepth = AssistVersions.JsonMaxDepth
            });
            return InferenceResultMapper.Map(parsed.RootElement, request, isFake: false);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return RulesEngine.Abstain(request.EntryId, "invalid-json");
        }
    }
}
