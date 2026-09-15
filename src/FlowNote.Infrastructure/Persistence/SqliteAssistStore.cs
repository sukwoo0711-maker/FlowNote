using System.Text.Json;
using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Core.Time;
using Microsoft.Data.Sqlite;

namespace FlowNote.Infrastructure.Persistence;

public sealed class SqliteAssistStore
{
    public const string ModeKey = "assist.mode";
    public const string PolicyKey = "assist.policy_revision";
    public const string AutoApplyKey = "assist.semantic_auto_apply";
    public const string BaseUrlKey = "assist.ollama_base_url";
    public const string ModelTagKey = "assist.model_tag";
    public const string DigestKey = "assist.locked_digest";
    public const string ScopeAckKey = "assist.scope_ack";
    public const string ProviderKey = "assist.provider";

    private readonly SqliteDatabaseExecutor _executor;
    private readonly IClock _clock;

    public SqliteAssistStore(SqliteDatabaseExecutor executor, IClock clock)
    {
        _executor = executor;
        _clock = clock;
    }

    public AssistSettings GetSettings()
        => _executor.Read(connection => ReadSettings(connection, null));

    public void SetMode(AssistMode mode)
    {
        _executor.Write((connection, transaction) =>
        {
            var current = ReadSettings(connection, transaction);
            WriteSetting(connection, transaction, ModeKey, AssistCodec.Mode(mode));
            WriteSetting(connection, transaction, PolicyKey, (current.PolicyRevision + 1).ToString());
            return 0;
        });
    }

    public void SetLockedDigest(string digest)
    {
        _executor.Write((connection, transaction) =>
        {
            WriteSetting(connection, transaction, DigestKey, digest);
            WriteSetting(connection, transaction, ProviderKey, AssistVersions.EmbeddedProvider);
            WriteSetting(connection, transaction, ModelTagKey, AssistVersions.ModelTag);
            return 0;
        });
    }

    public void AcknowledgeScope()
    {
        _executor.Write((connection, transaction) =>
        {
            WriteSetting(connection, transaction, ScopeAckKey, "true");
            return 0;
        });
    }

    public bool ScopeAcknowledged()
        => _executor.Read(connection => ReadSetting(connection, null, ScopeAckKey) == "true");

    public void OnNoteSaved(SqliteConnection connection, SqliteTransaction transaction, TimelineEntry entry)
    {
        var settings = ReadSettings(connection, transaction);
        var revision = AssistText.EntryRevision(entry);
        var assignment = ReadAssignment(connection, transaction, entry.Id);
        if (entry.WorkItemId is not null)
        {
            var threadId = EnsureThreadForWorkItem(connection, transaction, entry.WorkItemId);
            WriteAssignment(connection, transaction, new EntryContextAssignment
            {
                EntryId = entry.Id,
                SourceRevision = revision,
                ThreadId = threadId,
                Origin = AssignmentOrigin.User,
                Resolution = AssignmentResolution.Assigned,
                Role = ContextRole.Unknown,
                RoleOrigin = AssignmentOrigin.User,
                SourceQuote = null,
                AnalysisRunId = null,
                UserLocked = true,
                CorrectionRevision = assignment?.CorrectionRevision ?? 0
            });
        }

        EnqueueJob(connection, transaction, entry, revision, settings, assignment?.CorrectionRevision ?? 0);
    }

    public void OnNoteUpdated(SqliteConnection connection, SqliteTransaction transaction, TimelineEntry entry)
    {
        var settings = ReadSettings(connection, transaction);
        var revision = AssistText.EntryRevision(entry);
        var assignment = ReadAssignment(connection, transaction, entry.Id);
        StalePendingJobs(connection, transaction, entry.Id, revision);
        StaleCandidates(connection, transaction, entry.Id);
        if (assignment is { UserLocked: true } or { Resolution: AssignmentResolution.ManualClear })
        {
            return;
        }

        EnqueueJob(connection, transaction, entry, revision, settings, assignment?.CorrectionRevision ?? 0);
    }

    public void OnNoteRelinked(SqliteConnection connection, SqliteTransaction transaction, TimelineEntry entry)
    {
        var revision = AssistText.EntryRevision(entry);
        var current = ReadAssignment(connection, transaction, entry.Id);
        var nextRevision = (current?.CorrectionRevision ?? 0) + 1;
        if (entry.WorkItemId is null)
        {
            var cleared = new EntryContextAssignment
            {
                EntryId = entry.Id,
                SourceRevision = revision,
                ThreadId = null,
                Origin = AssignmentOrigin.User,
                Resolution = AssignmentResolution.ManualClear,
                Role = ContextRole.Unknown,
                RoleOrigin = AssignmentOrigin.User,
                SourceQuote = null,
                AnalysisRunId = null,
                UserLocked = true,
                CorrectionRevision = nextRevision
            };
            InsertCorrection(connection, transaction, entry.Id, Guid.NewGuid().ToString("D"), current, cleared, nextRevision, "user_cleared");
            WriteAssignment(connection, transaction, cleared);
            StalePendingJobs(connection, transaction, entry.Id, revision);
            return;
        }

        var threadId = EnsureThreadForWorkItem(connection, transaction, entry.WorkItemId);
        var next = new EntryContextAssignment
        {
            EntryId = entry.Id,
            SourceRevision = revision,
            ThreadId = threadId,
            Origin = AssignmentOrigin.User,
            Resolution = AssignmentResolution.Assigned,
            Role = ContextRole.Unknown,
            RoleOrigin = AssignmentOrigin.User,
            SourceQuote = null,
            AnalysisRunId = null,
            UserLocked = true,
            CorrectionRevision = nextRevision
        };
        InsertCorrection(connection, transaction, entry.Id, Guid.NewGuid().ToString("D"), current, next, nextRevision, "user_changed");
        WriteAssignment(connection, transaction, next);
        StalePendingJobs(connection, transaction, entry.Id, revision);
    }

    public void OnNoteDeleted(SqliteConnection connection, SqliteTransaction transaction, string entryId)
    {
        StalePendingJobs(connection, transaction, entryId, "deleted");
        StaleCandidates(connection, transaction, entryId);
    }

    public EntryContextAssignment? GetAssignment(string entryId)
        => _executor.Read(connection => ReadAssignment(connection, null, entryId));

    public IReadOnlyDictionary<string, EntryContextAssignment> ListAssignments(IReadOnlyList<string> entryIds)
    {
        if (entryIds.Count == 0)
        {
            return new Dictionary<string, EntryContextAssignment>(StringComparer.Ordinal);
        }

        return _executor.Read(connection =>
        {
            var map = new Dictionary<string, EntryContextAssignment>(StringComparer.Ordinal);
            foreach (var id in entryIds)
            {
                var item = ReadAssignment(connection, null, id);
                if (item is not null)
                {
                    map[id] = item;
                }
            }

            return (IReadOnlyDictionary<string, EntryContextAssignment>)map;
        });
    }

    public IReadOnlyDictionary<string, ContextThread> ListThreadsById(IEnumerable<string> ids)
    {
        return _executor.Read(connection =>
        {
            var map = new Dictionary<string, ContextThread>(StringComparer.Ordinal);
            foreach (var id in ids.Distinct(StringComparer.Ordinal))
            {
                var thread = ReadThread(connection, null, id);
                if (thread is not null)
                {
                    map[id] = thread;
                }
            }

            return (IReadOnlyDictionary<string, ContextThread>)map;
        });
    }

    public IReadOnlyList<ContextThread> ListThreads()
        => _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM context_threads ORDER BY created_at_utc ASC;";
            using var reader = command.ExecuteReader();
            var list = new List<ContextThread>();
            while (reader.Read())
            {
                list.Add(MapThread(reader));
            }

            return (IReadOnlyList<ContextThread>)list;
        });

    public IReadOnlyList<AnalysisJob> ListJobs(string entryId)
        => _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM analysis_jobs WHERE entry_id = $id ORDER BY created_at_utc ASC;";
            command.Parameters.AddWithValue("$id", entryId);
            using var reader = command.ExecuteReader();
            var list = new List<AnalysisJob>();
            while (reader.Read())
            {
                list.Add(MapJob(reader));
            }

            return (IReadOnlyList<AnalysisJob>)list;
        });

    public AnalysisJob? LatestJob(string entryId)
        => ListJobs(entryId).LastOrDefault();

    public IReadOnlyList<ContextMention> ListMentions(string entryId)
        => _executor.Read(connection => ListMentions(connection, null, entryId));

    public IReadOnlyList<ActionCandidate> ListActionCandidates(string entryId)
        => _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = null;
            command.CommandText = "SELECT * FROM action_candidates WHERE entry_id = $id ORDER BY id;";
            command.Parameters.AddWithValue("$id", entryId);
            using var reader = command.ExecuteReader();
            var list = new List<ActionCandidate>();
            while (reader.Read())
            {
                list.Add(MapCandidate(reader));
            }

            return (IReadOnlyList<ActionCandidate>)list;
        });

    public IReadOnlyList<ThreadCandidate> ListCandidatesFor(TimelineEntry entry)
        => _executor.Read(connection => BuildCandidates(connection, entry));

    public AnalysisJob? ClaimNextJob(DateTimeOffset nowUtc)
    {
        return _executor.Write((connection, transaction) =>
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = """
                SELECT j.* FROM analysis_jobs j
                LEFT JOIN entries e ON e.id = j.entry_id
                WHERE j.attempts < $max
                  AND (
                    j.status = 'pending'
                    OR j.status = 'retry_wait'
                    OR (j.status = 'running' AND j.lease_until_utc IS NOT NULL AND j.lease_until_utc < $now)
                  )
                  AND (j.next_attempt_utc IS NULL OR j.next_attempt_utc <= $now)
                ORDER BY COALESCE(e.occurred_at_utc, j.created_at_utc) ASC,
                         COALESCE(e.seq, 0) ASC,
                         j.created_at_utc ASC
                LIMIT 1;
                """;
            select.Parameters.AddWithValue("$max", AssistVersions.MaxAttempts);
            select.Parameters.AddWithValue("$now", UtcInstant.ToStorage(nowUtc));
            AnalysisJob? job;
            using (var reader = select.ExecuteReader())
            {
                if (!reader.Read())
                {
                    return null;
                }

                job = MapJob(reader);
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE analysis_jobs
                SET status = 'running',
                    attempts = attempts + 1,
                    lease_until_utc = $lease,
                    updated_at_utc = $now
                WHERE id = $id;
                """;
            update.Parameters.AddWithValue("$lease", UtcInstant.ToStorage(nowUtc.AddSeconds(AssistVersions.LeaseSeconds)));
            update.Parameters.AddWithValue("$now", UtcInstant.ToStorage(nowUtc));
            update.Parameters.AddWithValue("$id", job.Id);
            update.ExecuteNonQuery();
            return ReadJob(connection, transaction, job.Id);
        });
    }

    public void FinishJob(string jobId, AnalysisJobStatus status, string? errorCode, string? resultJson, TimeSpan elapsed)
    {
        _executor.Write((connection, transaction) =>
        {
            var now = UtcInstant.ToStorage(_clock.UtcNow);
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE analysis_jobs
                SET status = $status,
                    error_code = $error,
                    lease_until_utc = NULL,
                    next_attempt_utc = $next,
                    updated_at_utc = $now
                WHERE id = $id;
                """;
            update.Parameters.AddWithValue("$status", AssistCodec.JobStatus(status));
            update.Parameters.AddWithValue("$error", (object?)errorCode ?? DBNull.Value);
            DateTimeOffset? next = null;
            if (status == AnalysisJobStatus.RetryWait)
            {
                var job = ReadJob(connection, transaction, jobId);
                var delay = job is { Attempts: <= 1 } ? 5 : 30;
                next = _clock.UtcNow.AddSeconds(delay);
            }

            update.Parameters.AddWithValue("$next", next is null ? DBNull.Value : UtcInstant.ToStorage(next.Value));
            update.Parameters.AddWithValue("$now", now);
            update.Parameters.AddWithValue("$id", jobId);
            update.ExecuteNonQuery();

            using var run = connection.CreateCommand();
            run.Transaction = transaction;
            run.CommandText = """
                INSERT INTO analysis_runs(
                  id, job_id, started_at_utc, finished_at_utc, elapsed_ms, model_tag, model_digest,
                  prompt_version, rules_version, result_json, error_code)
                VALUES ($id, $job, $start, $end, $elapsed, $tag, $digest, $prompt, $rules, $result, $error);
                """;
            run.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            run.Parameters.AddWithValue("$job", jobId);
            run.Parameters.AddWithValue("$start", now);
            run.Parameters.AddWithValue("$end", now);
            run.Parameters.AddWithValue("$elapsed", (int)elapsed.TotalMilliseconds);
            var settings = ReadSettings(connection, transaction);
            run.Parameters.AddWithValue("$tag", settings.ModelTag);
            run.Parameters.AddWithValue("$digest", (object?)settings.LockedDigest ?? settings.ModelTag);
            run.Parameters.AddWithValue("$prompt", PromptCatalog.PromptVersion);
            run.Parameters.AddWithValue("$rules", AssistVersions.RulesVersion);
            run.Parameters.AddWithValue("$result", (object?)resultJson ?? DBNull.Value);
            run.Parameters.AddWithValue("$error", (object?)errorCode ?? DBNull.Value);
            run.ExecuteNonQuery();
            return 0;
        });
    }

    public string ApplyInference(AnalysisJob job, TimelineEntry entry, InferenceResult result, AssignmentOrigin origin)
    {
        return _executor.Write((connection, transaction) =>
        {
            var current = ReadAssignment(connection, transaction, entry.Id);
            if (current is not null && current.CorrectionRevision != job.CorrectionSnapshot)
            {
                FinishJobInTx(connection, transaction, job.Id, AnalysisJobStatus.Stale, "correction-changed", null);
                return "stale-correction";
            }

            if (!string.Equals(AssistText.EntryRevision(entry), job.EntryRevision, StringComparison.Ordinal))
            {
                FinishJobInTx(connection, transaction, job.Id, AnalysisJobStatus.Stale, "revision-changed", null);
                return "stale-revision";
            }

            if (entry.DeletedAtUtc is not null)
            {
                FinishJobInTx(connection, transaction, job.Id, AnalysisJobStatus.Stale, "deleted", null);
                return "stale-deleted";
            }

            var settings = ReadSettings(connection, transaction);
            if (settings.PolicyRevision != job.PolicyRevision)
            {
                FinishJobInTx(connection, transaction, job.Id, AnalysisJobStatus.Stale, "policy-changed", null);
                return "stale-policy";
            }

            var error = InferenceValidator.Validate(new InferenceRequest
            {
                EntryId = entry.Id,
                NoteText = entry.Body,
                EntryRevision = job.EntryRevision,
                CorrectionRevision = job.CorrectionSnapshot,
                PolicyRevision = job.PolicyRevision,
                Candidates = BuildCandidates(connection, entry)
            }, result);
            if (error is not null)
            {
                FinishJobInTx(connection, transaction, job.Id, AnalysisJobStatus.Failed, error, null);
                return error;
            }

            if (!AssistPolicy.CanApply(settings, current, result, result.ErrorCode))
            {
                var blocked = current is { UserLocked: true } or { Resolution: AssignmentResolution.ManualClear }
                    ? AnalysisJobStatus.Stale
                    : AnalysisJobStatus.Abstained;
                FinishJobInTx(connection, transaction, job.Id, blocked, result.ErrorCode ?? "policy-rejected", null);
                return "policy-rejected";
            }

            ApplyResult(connection, transaction, entry, result, origin, job.Id);
            var status = result.Decision == AssistDecision.Abstain ? AnalysisJobStatus.Abstained : AnalysisJobStatus.Succeeded;
            FinishJobInTx(connection, transaction, job.Id, status, result.ErrorCode, JsonSerializer.Serialize(new
            {
                decision = AssistCodec.Decision(result.Decision),
                role = result.Primary is null ? null : AssistCodec.Role(result.Primary.Role)
            }));
            return "applied";
        });
    }

    public string ApplySynthetic(TimelineEntry entry, InferenceResult result, AssignmentOrigin origin)
    {
        return _executor.Write((connection, transaction) =>
        {
            ApplyResult(connection, transaction, entry, result, origin, analysisRunId: null);
            StalePendingJobs(connection, transaction, entry.Id, AssistText.EntryRevision(entry));
            return "applied";
        });
    }

    public ContextThread CreateThread(string title, string? sourceEntryId, string? sourceRevision, TitleOrigin origin, string? issueKey = null, string? workItemId = null)
    {
        return _executor.Write((connection, transaction) =>
            InsertThread(connection, transaction, title, sourceEntryId, sourceRevision, origin, issueKey, workItemId));
    }

    public void RegisterAlias(string alias, string threadId)
    {
        _executor.Write((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO context_aliases(alias, thread_id) VALUES ($alias, $thread)
                ON CONFLICT(alias) DO UPDATE SET thread_id = excluded.thread_id;
                """;
            command.Parameters.AddWithValue("$alias", alias);
            command.Parameters.AddWithValue("$thread", threadId);
            command.ExecuteNonQuery();
            return 0;
        });
    }

    public void CorrectAssignment(string entryId, string requestId, string? threadId, bool createNew, string? newTitle)
    {
        _executor.Write((connection, transaction) =>
        {
            var entry = SqliteEntryService.FindById(connection, transaction, entryId)
                ?? throw new InvalidOperationException("기록을 찾을 수 없습니다.");
            var current = ReadAssignment(connection, transaction, entryId);
            var nextRevision = (current?.CorrectionRevision ?? 0) + 1;
            string? resolvedThread = threadId;
            if (createNew)
            {
                var title = string.IsNullOrWhiteSpace(newTitle)
                    ? TruncateTitle(entry.Body)
                    : newTitle.Trim();
                resolvedThread = InsertThread(connection, transaction, title, entry.Id, AssistText.EntryRevision(entry), TitleOrigin.User, null, null).Id;
            }

            var next = new EntryContextAssignment
            {
                EntryId = entryId,
                SourceRevision = AssistText.EntryRevision(entry),
                ThreadId = resolvedThread,
                Origin = AssignmentOrigin.User,
                Resolution = resolvedThread is null ? AssignmentResolution.ManualClear : AssignmentResolution.Assigned,
                Role = ContextRole.Unknown,
                RoleOrigin = AssignmentOrigin.User,
                SourceQuote = current?.SourceQuote,
                AnalysisRunId = null,
                UserLocked = true,
                CorrectionRevision = nextRevision
            };
            InsertCorrection(connection, transaction, entryId, requestId, current, next, nextRevision, resolvedThread is null ? "user_cleared" : "user_changed");
            WriteAssignment(connection, transaction, next);
            StalePendingJobs(connection, transaction, entryId, next.SourceRevision);
            return 0;
        });
    }

    public void UndoCorrection(string entryId, string requestId)
    {
        _executor.Write((connection, transaction) =>
        {
            using var select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText = """
                SELECT previous_json, expected_version FROM context_corrections
                WHERE entry_id = $entry
                ORDER BY changed_at_utc DESC LIMIT 1;
                """;
            select.Parameters.AddWithValue("$entry", entryId);
            using var reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return 0;
            }

            var previous = JsonSerializer.Deserialize<AssignmentSnap>(reader.GetString(0));
            var expected = reader.GetInt32(1);
            reader.Close();
            if (previous is null)
            {
                return 0;
            }

            var entry = SqliteEntryService.FindById(connection, transaction, entryId);
            if (entry is null)
            {
                return 0;
            }

            var current = ReadAssignment(connection, transaction, entryId);
            var restored = previous.ToAssignment(entryId, AssistText.EntryRevision(entry));
            restored = new EntryContextAssignment
            {
                EntryId = restored.EntryId,
                SourceRevision = restored.SourceRevision,
                ThreadId = restored.ThreadId,
                Origin = restored.Origin,
                Resolution = restored.Resolution,
                Role = restored.Role,
                RoleOrigin = restored.RoleOrigin,
                SourceQuote = restored.SourceQuote,
                AnalysisRunId = restored.AnalysisRunId,
                UserLocked = restored.UserLocked,
                CorrectionRevision = expected + 1
            };
            InsertCorrection(connection, transaction, entryId, requestId, current, restored, expected + 1, "undo");
            WriteAssignment(connection, transaction, restored);
            return 0;
        });
    }

    public string? Provenance(string entryId)
    {
        var assignment = GetAssignment(entryId);
        if (assignment is null)
        {
            return null;
        }

        var thread = assignment.ThreadId is null ? null : ListThreadsById([assignment.ThreadId]).GetValueOrDefault(assignment.ThreadId);
        var title = thread?.Title ?? "연결 없음";
        return $"{AssistCodec.OriginLabel(assignment.Origin)} · {AssistCodec.RoleLabel(assignment.Role)} · {title}";
    }

    private void ApplyResult(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TimelineEntry entry,
        InferenceResult result,
        AssignmentOrigin origin,
        string? analysisRunId)
    {
        var revision = AssistText.EntryRevision(entry);
        var current = ReadAssignment(connection, transaction, entry.Id);
        string? threadId = null;
        var role = ContextRole.Unknown;
        var resolution = AssignmentResolution.Abstained;
        string? quote = null;

        if (result.Decision == AssistDecision.Link && result.Primary?.ThreadId is not null)
        {
            threadId = result.Primary.ThreadId;
            role = result.Primary.Role;
            resolution = AssignmentResolution.Assigned;
            quote = result.Primary.SourceQuote;
        }
        else if (result.Decision == AssistDecision.New && result.Primary?.TopicQuote is not null)
        {
            var created = InsertThread(
                connection,
                transaction,
                result.Primary.TopicQuote,
                entry.Id,
                revision,
                TitleOrigin.Derived,
                null,
                null);
            threadId = created.Id;
            role = result.Primary.Role;
            resolution = AssignmentResolution.Assigned;
            quote = result.Primary.SourceQuote;
        }

        WriteAssignment(connection, transaction, new EntryContextAssignment
        {
            EntryId = entry.Id,
            SourceRevision = revision,
            ThreadId = threadId,
            Origin = origin,
            Resolution = resolution,
            Role = role,
            RoleOrigin = origin,
            SourceQuote = quote,
            AnalysisRunId = analysisRunId,
            UserLocked = false,
            CorrectionRevision = current?.CorrectionRevision ?? 0
        });

        if (threadId is not null && resolution == AssignmentResolution.Assigned)
        {
            foreach (var alias in AssistText.AliasSeeds(entry.Body).Append(result.Primary?.TopicQuote).OfType<string>())
            {
                WriteAlias(connection, transaction, alias, threadId);
            }
        }

        DeleteMentions(connection, transaction, entry.Id);
        foreach (var mention in result.Mentions)
        {
            var mentionThread = mention.ThreadId;
            if (mentionThread is null && mention.TopicQuote is not null)
            {
                mentionThread = InsertThread(connection, transaction, mention.TopicQuote, entry.Id, revision, TitleOrigin.Derived, null, null).Id;
            }

            if (mentionThread is null)
            {
                continue;
            }

            InsertMention(connection, transaction, entry.Id, mentionThread, mention, revision, origin);
            InsertActionCandidate(connection, transaction, entry.Id, mentionThread, mention.SourceQuote, revision, ActionCandidateKind.Request);
            if (mention.NextActionQuote is not null)
            {
                InsertActionCandidate(connection, transaction, entry.Id, mentionThread, mention.NextActionQuote, revision, ActionCandidateKind.NextAction);
            }
        }

        if (result.Primary?.NextActionQuote is not null && threadId is not null)
        {
            InsertActionCandidate(connection, transaction, entry.Id, threadId, result.Primary.NextActionQuote, revision, ActionCandidateKind.NextAction);
        }

        if (result.Primary?.Role == ContextRole.CompletionMention && threadId is not null && result.Primary.SourceQuote is not null)
        {
            InsertActionCandidate(connection, transaction, entry.Id, threadId, result.Primary.SourceQuote, revision, ActionCandidateKind.CompletionMention);
        }

        if (result.Primary?.Role is ContextRole.RequestLater or ContextRole.RequestUnknown && threadId is not null && result.Primary.SourceQuote is not null)
        {
            InsertActionCandidate(connection, transaction, entry.Id, threadId, result.Primary.SourceQuote, revision, ActionCandidateKind.Request);
        }
    }

    private static IReadOnlyList<ThreadCandidate> BuildCandidates(SqliteConnection connection, TimelineEntry entry)
    {
        var rows = new List<(string Id, string Title, string? IssueKey)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT t.id, t.title, t.issue_key
                FROM context_threads t
                LEFT JOIN entry_context_assignments a ON a.thread_id = t.id
                LEFT JOIN entries e ON e.id = a.entry_id
                GROUP BY t.id
                ORDER BY MAX(e.occurred_at_utc) DESC, t.id ASC
                LIMIT 30;
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
        }

        var pool = rows.Select(row => new ThreadCandidate
        {
            ThreadId = row.Id,
            Title = row.Title,
            IssueKey = row.IssueKey,
            Excerpts = ReadExcerpts(connection, row.Id, entry),
            Aliases = ReadAliases(connection, row.Id)
        }).ToList();

        return CandidateSelector.Rank(entry.Body, pool);
    }

    private static IReadOnlyList<string> ReadExcerpts(SqliteConnection connection, string threadId, TimelineEntry current)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.body
            FROM entries e
            JOIN entry_context_assignments a ON a.entry_id = e.id
            WHERE a.thread_id = $thread
              AND e.deleted_at_utc IS NULL
              AND (e.occurred_at_utc < $occurred OR (e.occurred_at_utc = $occurred AND e.seq < $seq))
            ORDER BY e.occurred_at_utc DESC, e.seq DESC
            LIMIT 2;
            """;
        command.Parameters.AddWithValue("$thread", threadId);
        command.Parameters.AddWithValue("$occurred", UtcInstant.ToStorage(current.OccurredAtUtc));
        command.Parameters.AddWithValue("$seq", current.Seq);
        using var reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
        {
            var body = reader.GetString(0);
            list.Add(body.Length <= 120 ? body : body[..120]);
        }

        return list;
    }

    private static IReadOnlyList<string> ReadAliases(SqliteConnection connection, string threadId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT alias FROM context_aliases WHERE thread_id = $id;";
        command.Parameters.AddWithValue("$id", threadId);
        using var reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
        {
            list.Add(reader.GetString(0));
        }

        return list;
    }

    private void EnqueueJob(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TimelineEntry entry,
        string revision,
        AssistSettings settings,
        int correctionSnapshot)
    {
        if (settings.Mode == AssistMode.Off)
        {
            return;
        }

        var digest = settings.Mode == AssistMode.LocalAssist
            ? settings.LockedDigest ?? settings.ModelTag
            : AssistVersions.RulesDigestToken;
        var key = AnalysisJobKey.Compute(
            entry.Id,
            revision,
            settings.PolicyRevision,
            AssistVersions.RulesVersion,
            PromptCatalog.PromptVersion,
            digest);
        var now = UtcInstant.ToStorage(_clock.UtcNow);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO analysis_jobs(
              id, job_key, entry_id, entry_revision, policy_revision, rules_version, prompt_version,
              model_digest, status, attempts, lease_until_utc, next_attempt_utc, error_code,
              correction_snapshot, created_at_utc, updated_at_utc)
            VALUES (
              $id, $key, $entry, $rev, $policy, $rules, $prompt, $digest, 'pending', 0, NULL, NULL, NULL,
              $correction, $now, $now)
            ON CONFLICT(job_key) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$entry", entry.Id);
        command.Parameters.AddWithValue("$rev", revision);
        command.Parameters.AddWithValue("$policy", settings.PolicyRevision);
        command.Parameters.AddWithValue("$rules", AssistVersions.RulesVersion);
        command.Parameters.AddWithValue("$prompt", PromptCatalog.PromptVersion);
        command.Parameters.AddWithValue("$digest", digest);
        command.Parameters.AddWithValue("$correction", correctionSnapshot);
        command.Parameters.AddWithValue("$now", now);
        command.ExecuteNonQuery();
    }

    private static void StalePendingJobs(SqliteConnection connection, SqliteTransaction transaction, string entryId, string _)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE analysis_jobs
            SET status = 'stale', updated_at_utc = $now
            WHERE entry_id = $id AND status IN ('pending', 'retry_wait', 'running', 'blocked');
            """;
        command.Parameters.AddWithValue("$id", entryId);
        command.Parameters.AddWithValue("$now", UtcInstant.ToStorage(DateTimeOffset.UtcNow));
        command.ExecuteNonQuery();
    }

    private static void StaleCandidates(SqliteConnection connection, SqliteTransaction transaction, string entryId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE action_candidates SET state = 'stale' WHERE entry_id = $id AND state = 'suggested';";
        command.Parameters.AddWithValue("$id", entryId);
        command.ExecuteNonQuery();
    }

    private string EnsureThreadForWorkItem(SqliteConnection connection, SqliteTransaction transaction, string workItemId)
    {
        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT * FROM context_threads WHERE work_item_id = $id LIMIT 1;";
        select.Parameters.AddWithValue("$id", workItemId);
        using (var reader = select.ExecuteReader())
        {
            if (reader.Read())
            {
                return reader.GetString(reader.GetOrdinal("id"));
            }
        }

        using var work = connection.CreateCommand();
        work.Transaction = transaction;
        work.CommandText = "SELECT title, issue_key FROM work_items WHERE id = $id LIMIT 1;";
        work.Parameters.AddWithValue("$id", workItemId);
        using var workReader = work.ExecuteReader();
        if (!workReader.Read())
        {
            throw new InvalidOperationException("연결할 할 일을 찾을 수 없습니다.");
        }

        var title = workReader.GetString(0);
        var issue = workReader.IsDBNull(1) ? null : workReader.GetString(1);
        workReader.Close();
        return InsertThread(connection, transaction, title, null, null, TitleOrigin.User, issue, workItemId).Id;
    }

    private ContextThread InsertThread(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string title,
        string? sourceEntryId,
        string? sourceRevision,
        TitleOrigin origin,
        string? issueKey,
        string? workItemId)
    {
        var id = Guid.NewGuid().ToString("D");
        var now = UtcInstant.ToStorage(_clock.UtcNow);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO context_threads(
              id, title, title_source_entry_id, title_source_revision, title_origin, work_item_id,
              created_at_utc, version, issue_key)
            VALUES ($id, $title, $source, $rev, $origin, $work, $created, 1, $issue);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$source", (object?)sourceEntryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$rev", (object?)sourceRevision ?? DBNull.Value);
        command.Parameters.AddWithValue("$origin", AssistCodec.FormatTitleOrigin(origin));
        command.Parameters.AddWithValue("$work", (object?)workItemId ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", now);
        command.Parameters.AddWithValue("$issue", (object?)issueKey ?? DBNull.Value);
        command.ExecuteNonQuery();
        if (!string.IsNullOrWhiteSpace(title) && title.Length >= 2)
        {
            WriteAlias(connection, transaction, title, id);
        }

        return ReadThread(connection, transaction, id) ?? throw new InvalidOperationException("thread insert failed");
    }

    private static void WriteAlias(SqliteConnection connection, SqliteTransaction transaction, string alias, string threadId)
    {
        if (string.IsNullOrWhiteSpace(alias) || alias.Length > AssistVersions.TopicQuoteMax)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO context_aliases(alias, thread_id) VALUES ($alias, $thread)
            ON CONFLICT(alias) DO UPDATE SET thread_id = excluded.thread_id;
            """;
        command.Parameters.AddWithValue("$alias", alias);
        command.Parameters.AddWithValue("$thread", threadId);
        command.ExecuteNonQuery();
    }

    private static void WriteAssignment(SqliteConnection connection, SqliteTransaction transaction, EntryContextAssignment assignment)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO entry_context_assignments(
              entry_id, source_revision, thread_id, origin, resolution, role, role_origin,
              source_quote, analysis_run_id, user_locked, correction_revision)
            VALUES (
              $entry, $rev, $thread, $origin, $resolution, $role, $role_origin,
              $quote, $run, $locked, $correction)
            ON CONFLICT(entry_id) DO UPDATE SET
              source_revision = excluded.source_revision,
              thread_id = excluded.thread_id,
              origin = excluded.origin,
              resolution = excluded.resolution,
              role = excluded.role,
              role_origin = excluded.role_origin,
              source_quote = excluded.source_quote,
              analysis_run_id = excluded.analysis_run_id,
              user_locked = excluded.user_locked,
              correction_revision = excluded.correction_revision;
            """;
        command.Parameters.AddWithValue("$entry", assignment.EntryId);
        command.Parameters.AddWithValue("$rev", assignment.SourceRevision);
        command.Parameters.AddWithValue("$thread", (object?)assignment.ThreadId ?? DBNull.Value);
        command.Parameters.AddWithValue("$origin", AssistCodec.Origin(assignment.Origin));
        command.Parameters.AddWithValue("$resolution", AssistCodec.Resolution(assignment.Resolution));
        command.Parameters.AddWithValue("$role", AssistCodec.Role(assignment.Role));
        command.Parameters.AddWithValue("$role_origin", AssistCodec.Origin(assignment.RoleOrigin));
        command.Parameters.AddWithValue("$quote", (object?)assignment.SourceQuote ?? DBNull.Value);
        command.Parameters.AddWithValue("$run", (object?)assignment.AnalysisRunId ?? DBNull.Value);
        command.Parameters.AddWithValue("$locked", assignment.UserLocked ? 1 : 0);
        command.Parameters.AddWithValue("$correction", assignment.CorrectionRevision);
        command.ExecuteNonQuery();
    }

    private static void InsertMention(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string entryId,
        string threadId,
        InferencePrimary mention,
        string revision,
        AssignmentOrigin origin)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO context_mentions(
              id, entry_id, thread_id, role, source_quote, source_revision, origin, next_action_quote)
            VALUES ($id, $entry, $thread, $role, $quote, $rev, $origin, $next);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$entry", entryId);
        command.Parameters.AddWithValue("$thread", threadId);
        command.Parameters.AddWithValue("$role", AssistCodec.Role(mention.Role));
        command.Parameters.AddWithValue("$quote", mention.SourceQuote);
        command.Parameters.AddWithValue("$rev", revision);
        command.Parameters.AddWithValue("$origin", AssistCodec.Origin(origin));
        command.Parameters.AddWithValue("$next", (object?)mention.NextActionQuote ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void DeleteMentions(SqliteConnection connection, SqliteTransaction transaction, string entryId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM context_mentions WHERE entry_id = $id;";
        command.Parameters.AddWithValue("$id", entryId);
        command.ExecuteNonQuery();
    }

    private static void InsertActionCandidate(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string entryId,
        string threadId,
        string quote,
        string revision,
        ActionCandidateKind kind)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO action_candidates(
              id, thread_id, entry_id, source_revision, source_quote, kind, state, linked_work_item_id,
              accepted_by_user_at_utc, version)
            VALUES ($id, $thread, $entry, $rev, $quote, $kind, 'suggested', NULL, NULL, 1);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$thread", threadId);
        command.Parameters.AddWithValue("$entry", entryId);
        command.Parameters.AddWithValue("$rev", revision);
        command.Parameters.AddWithValue("$quote", quote);
        command.Parameters.AddWithValue("$kind", AssistCodec.CandidateKind(kind));
        command.ExecuteNonQuery();
    }

    private void InsertCorrection(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string entryId,
        string requestId,
        EntryContextAssignment? previous,
        EntryContextAssignment next,
        int expected,
        string reason)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO context_corrections(
              id, request_id, entry_id, previous_json, next_json, changed_at_utc, expected_version, reason)
            VALUES ($id, $request, $entry, $prev, $next, $changed, $expected, $reason)
            ON CONFLICT(request_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$entry", entryId);
        command.Parameters.AddWithValue("$prev", JsonSerializer.Serialize(AssignmentSnap.From(previous)));
        command.Parameters.AddWithValue("$next", JsonSerializer.Serialize(AssignmentSnap.From(next)));
        command.Parameters.AddWithValue("$changed", UtcInstant.ToStorage(_clock.UtcNow));
        command.Parameters.AddWithValue("$expected", expected);
        command.Parameters.AddWithValue("$reason", reason);
        command.ExecuteNonQuery();
    }

    private void FinishJobInTx(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string jobId,
        AnalysisJobStatus status,
        string? error,
        string? resultJson)
    {
        var now = UtcInstant.ToStorage(_clock.UtcNow);
        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE analysis_jobs
            SET status = $status, error_code = $error, lease_until_utc = NULL, updated_at_utc = $now
            WHERE id = $id;
            """;
        update.Parameters.AddWithValue("$status", AssistCodec.JobStatus(status));
        update.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        update.Parameters.AddWithValue("$now", now);
        update.Parameters.AddWithValue("$id", jobId);
        update.ExecuteNonQuery();
        _ = resultJson;
    }

    internal AssistSettings ReadSettings(SqliteConnection connection, SqliteTransaction? transaction)
    {
        var mode = AssistCodec.ParseMode(ReadSetting(connection, transaction, ModeKey));
        var policyText = ReadSetting(connection, transaction, PolicyKey);
        var policy = int.TryParse(policyText, out var parsed) ? parsed : 1;
        var auto = ReadSetting(connection, transaction, AutoApplyKey) == "true";
        return new AssistSettings
        {
            Mode = mode,
            PolicyRevision = policy,
            SemanticAutoApply = auto,
            OllamaBaseUrl = ReadSetting(connection, transaction, BaseUrlKey) ?? AssistVersions.DefaultOllamaBaseUrl,
            ModelTag = ReadSetting(connection, transaction, ModelTagKey) ?? AssistVersions.ModelTag,
            LockedDigest = ReadSetting(connection, transaction, DigestKey)
        };
    }

    private static string? ReadSetting(SqliteConnection connection, SqliteTransaction? transaction, string key)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT value_json FROM settings WHERE key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void WriteSetting(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO settings(key, value_json) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value_json = excluded.value_json;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static EntryContextAssignment? ReadAssignment(SqliteConnection connection, SqliteTransaction? transaction, string entryId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT * FROM entry_context_assignments WHERE entry_id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", entryId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapAssignment(reader) : null;
    }

    private static ContextThread? ReadThread(SqliteConnection connection, SqliteTransaction? transaction, string id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT * FROM context_threads WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapThread(reader) : null;
    }

    private static AnalysisJob? ReadJob(SqliteConnection connection, SqliteTransaction? transaction, string id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT * FROM analysis_jobs WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapJob(reader) : null;
    }

    private static IReadOnlyList<ContextMention> ListMentions(SqliteConnection connection, SqliteTransaction? transaction, string entryId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT * FROM context_mentions WHERE entry_id = $id;";
        command.Parameters.AddWithValue("$id", entryId);
        using var reader = command.ExecuteReader();
        var list = new List<ContextMention>();
        while (reader.Read())
        {
            list.Add(new ContextMention
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                EntryId = reader.GetString(reader.GetOrdinal("entry_id")),
                ThreadId = reader.GetString(reader.GetOrdinal("thread_id")),
                Role = AssistCodec.ParseRole(reader.GetString(reader.GetOrdinal("role"))),
                SourceQuote = reader.GetString(reader.GetOrdinal("source_quote")),
                SourceRevision = reader.GetString(reader.GetOrdinal("source_revision")),
                Origin = AssistCodec.ParseOrigin(reader.GetString(reader.GetOrdinal("origin"))),
                NextActionQuote = SqliteRowMapper.GetNullString(reader, "next_action_quote")
            });
        }

        return list;
    }

    private static EntryContextAssignment MapAssignment(SqliteDataReader reader)
        => new()
        {
            EntryId = reader.GetString(reader.GetOrdinal("entry_id")),
            SourceRevision = reader.GetString(reader.GetOrdinal("source_revision")),
            ThreadId = SqliteRowMapper.GetNullString(reader, "thread_id"),
            Origin = AssistCodec.ParseOrigin(reader.GetString(reader.GetOrdinal("origin"))),
            Resolution = AssistCodec.ParseResolution(reader.GetString(reader.GetOrdinal("resolution"))),
            Role = AssistCodec.ParseRole(reader.GetString(reader.GetOrdinal("role"))),
            RoleOrigin = AssistCodec.ParseOrigin(reader.GetString(reader.GetOrdinal("role_origin"))),
            SourceQuote = SqliteRowMapper.GetNullString(reader, "source_quote"),
            AnalysisRunId = SqliteRowMapper.GetNullString(reader, "analysis_run_id"),
            UserLocked = reader.GetInt32(reader.GetOrdinal("user_locked")) != 0,
            CorrectionRevision = reader.GetInt32(reader.GetOrdinal("correction_revision"))
        };

    private static ContextThread MapThread(SqliteDataReader reader)
        => new()
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            TitleSourceEntryId = SqliteRowMapper.GetNullString(reader, "title_source_entry_id"),
            TitleSourceRevision = SqliteRowMapper.GetNullString(reader, "title_source_revision"),
            TitleOrigin = AssistCodec.ParseTitleOrigin(reader.GetString(reader.GetOrdinal("title_origin"))),
            WorkItemId = SqliteRowMapper.GetNullString(reader, "work_item_id"),
            CreatedAtUtc = UtcInstant.Parse(reader.GetString(reader.GetOrdinal("created_at_utc"))),
            Version = reader.GetInt32(reader.GetOrdinal("version")),
            IssueKey = SqliteRowMapper.GetNullString(reader, "issue_key")
        };

    private static AnalysisJob MapJob(SqliteDataReader reader)
        => new()
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            JobKey = reader.GetString(reader.GetOrdinal("job_key")),
            EntryId = reader.GetString(reader.GetOrdinal("entry_id")),
            EntryRevision = reader.GetString(reader.GetOrdinal("entry_revision")),
            PolicyRevision = reader.GetInt32(reader.GetOrdinal("policy_revision")),
            RulesVersion = reader.GetString(reader.GetOrdinal("rules_version")),
            PromptVersion = reader.GetString(reader.GetOrdinal("prompt_version")),
            ModelDigest = reader.GetString(reader.GetOrdinal("model_digest")),
            Status = AssistCodec.ParseJobStatus(reader.GetString(reader.GetOrdinal("status"))),
            Attempts = reader.GetInt32(reader.GetOrdinal("attempts")),
            LeaseUntilUtc = SqliteRowMapper.GetNullInstant(reader, "lease_until_utc"),
            NextAttemptUtc = SqliteRowMapper.GetNullInstant(reader, "next_attempt_utc"),
            ErrorCode = SqliteRowMapper.GetNullString(reader, "error_code"),
            CorrectionSnapshot = reader.GetInt32(reader.GetOrdinal("correction_snapshot")),
            CreatedAtUtc = UtcInstant.Parse(reader.GetString(reader.GetOrdinal("created_at_utc"))),
            UpdatedAtUtc = UtcInstant.Parse(reader.GetString(reader.GetOrdinal("updated_at_utc")))
        };

    private static ActionCandidate MapCandidate(SqliteDataReader reader)
        => new()
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            ThreadId = SqliteRowMapper.GetNullString(reader, "thread_id"),
            EntryId = reader.GetString(reader.GetOrdinal("entry_id")),
            SourceRevision = reader.GetString(reader.GetOrdinal("source_revision")),
            SourceQuote = reader.GetString(reader.GetOrdinal("source_quote")),
            Kind = AssistCodec.ParseCandidateKind(reader.GetString(reader.GetOrdinal("kind"))),
            State = AssistCodec.ParseCandidateState(reader.GetString(reader.GetOrdinal("state"))),
            LinkedWorkItemId = SqliteRowMapper.GetNullString(reader, "linked_work_item_id"),
            AcceptedByUserAtUtc = SqliteRowMapper.GetNullInstant(reader, "accepted_by_user_at_utc"),
            Version = reader.GetInt32(reader.GetOrdinal("version"))
        };

    private static string TruncateTitle(string body)
    {
        var text = body.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var line = text.Split('\n')[0];
        return line.Length <= 40 ? line : line[..40];
    }

    private sealed record AssignmentSnap(
        string? ThreadId,
        string Origin,
        string Resolution,
        string Role,
        string RoleOrigin,
        string? SourceQuote,
        bool UserLocked,
        int CorrectionRevision)
    {
        public static AssignmentSnap From(EntryContextAssignment? item)
            => item is null
                ? new AssignmentSnap(null, "user", "abstained", "UNKNOWN", "user", null, false, 0)
                : new AssignmentSnap(
                    item.ThreadId,
                    AssistCodec.Origin(item.Origin),
                    AssistCodec.Resolution(item.Resolution),
                    AssistCodec.Role(item.Role),
                    AssistCodec.Origin(item.RoleOrigin),
                    item.SourceQuote,
                    item.UserLocked,
                    item.CorrectionRevision);

        public EntryContextAssignment ToAssignment(string entryId, string revision)
            => new()
            {
                EntryId = entryId,
                SourceRevision = revision,
                ThreadId = ThreadId,
                Origin = AssistCodec.ParseOrigin(Origin),
                Resolution = AssistCodec.ParseResolution(Resolution),
                Role = AssistCodec.ParseRole(Role),
                RoleOrigin = AssistCodec.ParseOrigin(RoleOrigin),
                SourceQuote = SourceQuote,
                AnalysisRunId = null,
                UserLocked = UserLocked,
                CorrectionRevision = CorrectionRevision
            };
    }
}
