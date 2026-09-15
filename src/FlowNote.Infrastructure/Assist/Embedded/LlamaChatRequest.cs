using System.Text.Json;
using FlowNote.Core.Assist;

namespace FlowNote.Infrastructure.Assist.Embedded;

internal static class LlamaChatRequest
{
    public const string JsonObjectWithSchema = "json_object+schema";
    public const string JsonSchemaEnvelope = "json_schema";

    private static string _format = JsonObjectWithSchema;

    public static string ActiveFormat => _format;

    public static bool TryFallbackFormat()
    {
        if (string.Equals(Volatile.Read(ref _format), JsonObjectWithSchema, StringComparison.Ordinal))
        {
            _format = JsonSchemaEnvelope;
            return true;
        }

        return false;
    }

    public static Dictionary<string, object?> Create(string alias, string userJson, JsonElement schema)
    {
        object responseFormat = string.Equals(_format, JsonSchemaEnvelope, StringComparison.Ordinal)
            ? new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["json_schema"] = new Dictionary<string, object?>
                {
                    ["name"] = "flownote_inference",
                    ["strict"] = true,
                    ["schema"] = schema
                }
            }
            : new Dictionary<string, object?>
            {
                ["type"] = "json_object",
                ["schema"] = schema
            };

        return new Dictionary<string, object?>
        {
            ["model"] = alias,
            ["messages"] = new object[]
            {
                new { role = "system", content = PromptCatalog.SystemPrompt },
                new { role = "user", content = userJson }
            },
            ["stream"] = false,
            ["temperature"] = 0,
            ["seed"] = 42,
            ["max_tokens"] = 768,
            ["chat_template_kwargs"] = new { enable_thinking = false },
            ["response_format"] = responseFormat
        };
    }
}
