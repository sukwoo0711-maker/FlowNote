using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Core.Time;
using FlowNote.Infrastructure.Persistence;

namespace FlowNote.Infrastructure.Assist;

public sealed class AnalysisWorker : IDisposable
{
    private readonly FlowNoteDatabase _database;
    private readonly IContextInference _inference;
    private readonly IClock _clock;
    private readonly RulesEngine _rules = new();
    private readonly ManualResetEventSlim _wake = new(false);
    private int _consecutiveFailures;
    private DateTimeOffset _circuitUntil = DateTimeOffset.MinValue;
    private bool _disposed;

    public AnalysisWorker(FlowNoteDatabase database, IContextInference inference, IClock clock)
    {
        _database = database;
        _inference = inference;
        _clock = clock;
        _database.Assist.JobQueued += Pulse;
    }

    public event Action? Completed;

    public void Pulse()
    {
        try
        {
            _wake.Set();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            bool processed;
            try
            {
                processed = await ProcessOneAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!processed)
            {
                try
                {
                    await WaitForJobAsync(cancellationToken);
                    _wake.Reset();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task WaitForJobAsync(CancellationToken cancellationToken)
    {
        if (_wake.IsSet)
        {
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = ThreadPool.RegisterWaitForSingleObject(
            _wake.WaitHandle,
            static (state, _) => ((TaskCompletionSource)state!).TrySetResult(),
            tcs,
            TimeSpan.FromSeconds(2),
            executeOnlyOnce: true);
        await using var cancel = cancellationToken.Register(static state => ((TaskCompletionSource)state!).TrySetCanceled(), tcs);
        try
        {
            await tcs.Task.ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            wait.Unregister(null);
        }
    }

    public async Task<bool> ProcessOneAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var job = _database.Assist.ClaimNextJob(_clock.UtcNow);
        if (job is null)
        {
            return false;
        }

        var started = _clock.UtcNow;
        try
        {
            var entry = await _database.Entries.GetByIdAsync(job.EntryId, cancellationToken);
            if (entry is null || entry.DeletedAtUtc is not null)
            {
                _database.Assist.FinishJob(job.Id, AnalysisJobStatus.Stale, "missing-entry", null, _clock.UtcNow - started, job.Attempts);
                Completed?.Invoke();
                return true;
            }

            var settings = _database.Assist.GetSettings();
            if (settings.Mode == AssistMode.Off)
            {
                _database.Assist.FinishJob(job.Id, AnalysisJobStatus.Blocked, "assist-off", null, _clock.UtcNow - started, job.Attempts);
                Completed?.Invoke();
                return true;
            }

            var noteBytes = System.Text.Encoding.UTF8.GetByteCount(entry.Body);
            if (noteBytes > AssistVersions.MaxInputUtf8Bytes)
            {
                _database.Assist.FinishJob(job.Id, AnalysisJobStatus.Blocked, "analysis-too-long", null, _clock.UtcNow - started, job.Attempts);
                Completed?.Invoke();
                return true;
            }

            var assignment = _database.Assist.GetAssignment(entry.Id);
            var candidates = _database.Assist.ListCandidatesFor(entry);
            var request = new InferenceRequest
            {
                EntryId = entry.Id,
                NoteText = entry.Body,
                AttachmentNames = _database.Entries.ListAttachments(entry.Id).Select(static item => item.OriginalName).ToList(),
                Candidates = candidates,
                EntryRevision = job.EntryRevision,
                CorrectionRevision = job.CorrectionSnapshot,
                PolicyRevision = job.PolicyRevision,
                ContinuationThreadId = FindContinuationThreadId(entry)
            };

            var rules = _rules.Decide(
                request,
                assignment is { UserLocked: true },
                assignment is { Resolution: AssignmentResolution.ManualClear });

            InferenceResult result;
            AssignmentOrigin origin;
            if (rules.Decision == AssistDecision.Link)
            {
                result = rules;
                origin = AssignmentOrigin.Rule;
            }
            else if (settings.Mode != AssistMode.LocalAssist)
            {
                result = rules;
                origin = AssignmentOrigin.Rule;
            }
            else if (_clock.UtcNow < _circuitUntil)
            {
                _database.Assist.FinishJob(job.Id, AnalysisJobStatus.Blocked, "model-unavailable", null, _clock.UtcNow - started, job.Attempts);
                Completed?.Invoke();
                return true;
            }
            else
            {
                result = await _inference.InferAsync(request, cancellationToken);
                origin = result.IsFake ? AssignmentOrigin.Rule : AssignmentOrigin.Model;
                if (string.Equals(result.ErrorCode, "model-unavailable-retry", StringComparison.Ordinal))
                {
                    RegisterFailure();
                    _database.Assist.FinishJob(job.Id, AnalysisJobStatus.RetryWait, result.ErrorCode, null, _clock.UtcNow - started, job.Attempts);
                    Completed?.Invoke();
                    return true;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            _ = _database.Assist.ApplyInference(job, entry, result, origin);
            _consecutiveFailures = 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _database.Assist.FinishJob(job.Id, AnalysisJobStatus.RetryWait, "worker-stopped", null, _clock.UtcNow - started, job.Attempts);
            throw;
        }
        catch (OperationCanceledException)
        {
            RegisterFailure();
            _database.Assist.FinishJob(job.Id, AnalysisJobStatus.RetryWait, "model-timeout", null, _clock.UtcNow - started, job.Attempts);
        }
        catch (Exception)
        {
            RegisterFailure();
            _database.Assist.FinishJob(job.Id, AnalysisJobStatus.RetryWait, "worker-exception", null, _clock.UtcNow - started, job.Attempts);
        }

        Completed?.Invoke();
        return true;
    }

    public async Task DrainAsync(int maxJobs, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < maxJobs; i++)
        {
            if (!await ProcessOneAsync(cancellationToken))
            {
                break;
            }
        }
    }

    private string? FindContinuationThreadId(TimelineEntry entry)
    {
        var local = _database.DisplayTimeZone.GetLocalDate(entry.OccurredAtUtc);
        var day = _database.Entries.ListForLocalDateAsync(local).GetAwaiter().GetResult();
        foreach (var prior in day
            .Where(item => item.Id != entry.Id
                && item.Kind == EntryKind.Note
                && item.DeletedAtUtc is null
                && (item.OccurredAtUtc < entry.OccurredAtUtc
                    || (item.OccurredAtUtc == entry.OccurredAtUtc && item.Seq < entry.Seq)))
            .OrderByDescending(static item => item.OccurredAtUtc)
            .ThenByDescending(static item => item.Seq))
        {
            var priorAssignment = _database.Assist.GetAssignment(prior.Id);
            if (priorAssignment is { Resolution: AssignmentResolution.Assigned, ThreadId: { } threadId }
                && priorAssignment.Role is ContextRole.Performed or ContextRole.CompletionMention or ContextRole.Unknown)
            {
                return threadId;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _disposed = true;
        _database.Assist.JobQueued -= Pulse;
        _wake.Dispose();
        if (_inference is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void RegisterFailure()
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= AssistVersions.CircuitBreakAfter)
        {
            _circuitUntil = _clock.UtcNow.AddSeconds(AssistVersions.CircuitBreakSeconds);
            _consecutiveFailures = 0;
        }
    }
}

public sealed class DelayedFakeInference : IContextInference
{
    private readonly TimeSpan _delay;
    private readonly Func<InferenceRequest, InferenceResult> _decide;

    public DelayedFakeInference(TimeSpan delay, Func<InferenceRequest, InferenceResult>? decide = null)
    {
        _delay = delay;
        _decide = decide ?? (request => RulesEngine.Abstain(request.EntryId, "fake-abstain"));
        IsFake = true;
    }

    public bool IsFake { get; }

    public bool IsAvailable => true;

    public async Task<InferenceResult> InferAsync(InferenceRequest request, CancellationToken cancellationToken)
    {
        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken);
        }

        var result = _decide(request);
        return new InferenceResult
        {
            EntryId = result.EntryId,
            Decision = result.Decision,
            Primary = result.Primary,
            Mentions = result.Mentions,
            ErrorCode = result.ErrorCode,
            IsFake = true
        };
    }
}

public sealed class GateInference : IContextInference
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsFake => true;

    public bool IsAvailable => true;

    public async Task<InferenceResult> InferAsync(InferenceRequest request, CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        using var linked = cancellationToken.Register(() => Release.TrySetCanceled(cancellationToken));
        await Release.Task.ConfigureAwait(false);
        return new InferenceResult
        {
            EntryId = request.EntryId,
            Decision = AssistDecision.New,
            IsFake = true,
            Primary = new InferencePrimary
            {
                TopicQuote = AssistText.TopicQuote(request.NoteText) ?? "gate",
                Role = ContextRole.Performed,
                SourceQuote = AssistText.TopicQuote(request.NoteText) ?? request.NoteText
            }
        };
    }
}
