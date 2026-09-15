using System.Text;
using System.Text.Json;
using FlowNote.Core.Assist;

namespace FlowNote.Infrastructure.Assist.Embedded;

public sealed class ManagedLlamaInference : IContextInference, IDisposable
{
    private readonly EmbeddedEngineManager _manager;

    public ManagedLlamaInference(EmbeddedEngineManager manager)
    {
        _manager = manager;
    }

    public bool IsFake => false;

    public bool IsAvailable => _manager.LastCheck.Ready;

    public async Task<InferenceResult> InferAsync(InferenceRequest request, CancellationToken cancellationToken)
    {
        var assets = _manager.LastCheck;
        if (!assets.Ready)
        {
            return RulesEngine.Abstain(request.EntryId, assets.Reason ?? "needs-assets");
        }

        var candidates = CandidateSelector.FitBudget(request.NoteText, request.Candidates, AssistVersions.MaxInputUtf8Bytes);
        var user = new
        {
            entry_id = request.EntryId,
            note_text = request.NoteText,
            attachment_names = request.AttachmentNames,
            candidates = candidates.Select(item => new
            {
                thread_id = item.ThreadId,
                title = item.Title,
                excerpts = item.Excerpts,
                issue_key = item.IssueKey
            })
        };
        using var schema = JsonDocument.Parse(PromptCatalog.ResponseSchemaJson);
        var format = schema.RootElement.Clone();
        var userJson = JsonSerializer.Serialize(user);
        var counted = JsonSerializer.Serialize(new
        {
            messages = new object[]
            {
                new { role = "system", content = PromptCatalog.SystemPrompt },
                new { role = "user", content = userJson }
            },
            response_format = new { type = "json_object", schema = format }
        });
        if (Encoding.UTF8.GetByteCount(counted) > AssistVersions.MaxInputUtf8Bytes)
        {
            return RulesEngine.Abstain(request.EntryId, "analysis-too-long");
        }

        try
        {
            var raw = await ChatOnceAsync(userJson, format, cancellationToken);
            var parsed = OpenAiChatParser.Parse(raw, request);
            var invalid = InferenceValidator.Validate(request, parsed);
            if (invalid is not null)
            {
                return RulesEngine.Abstain(request.EntryId, invalid);
            }

            return parsed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("model-http-400", StringComparison.Ordinal) && LlamaChatRequest.TryFallbackFormat())
        {
            try
            {
                var raw = await ChatOnceAsync(userJson, format, cancellationToken);
                var parsed = OpenAiChatParser.Parse(raw, request);
                var invalid = InferenceValidator.Validate(request, parsed);
                return invalid is null ? parsed : RulesEngine.Abstain(request.EntryId, invalid);
            }
            catch (InvalidOperationException retryEx)
            {
                return RulesEngine.Abstain(request.EntryId, retryEx.Message);
            }
        }
        catch (InvalidOperationException ex)
        {
            return RulesEngine.Abstain(request.EntryId, ex.Message);
        }
        catch (HttpRequestException)
        {
            return RulesEngine.Abstain(request.EntryId, "model-unavailable-retry");
        }
    }

    public void Dispose()
    {
    }

    private Task<string> ChatOnceAsync(string userJson, JsonElement schema, CancellationToken cancellationToken)
        => _manager.ChatAsync(LlamaChatRequest.Create(_manager.CurrentAlias ?? "flownote-local", userJson, schema), cancellationToken);
}
