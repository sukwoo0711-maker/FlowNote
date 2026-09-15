using System.Text;
using System.Text.Json;

namespace FlowNote.Infrastructure.Assist;

public static class StrictJson
{
    public static JsonDocument Parse(string json)
    {
        EnsureNoDuplicateProperties(json);
        return JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            MaxDepth = 8
        });
    }

    public static void EnsureNoDuplicateProperties(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8
        });
        var stack = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    stack.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    stack.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    var name = reader.GetString() ?? "";
                    if (stack.Count == 0 || !stack.Peek().Add(name))
                    {
                        throw new InvalidOperationException("duplicate-property");
                    }

                    break;
            }
        }
    }
}
