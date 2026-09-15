namespace FlowNote.Core.Assist;

public sealed class UnavailableContextInference : IContextInference
{
    public static UnavailableContextInference Instance { get; } = new();

    public bool IsFake => false;

    public bool IsAvailable => false;

    public Task<InferenceResult> InferAsync(InferenceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new InferenceResult
        {
            EntryId = request.EntryId,
            Decision = AssistDecision.Abstain,
            ErrorCode = "model-unavailable"
        });
    }
}
