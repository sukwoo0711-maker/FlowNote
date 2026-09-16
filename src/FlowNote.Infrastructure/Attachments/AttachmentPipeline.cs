using System.Security.Cryptography;
using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Core.Time;
using FlowNote.Infrastructure.Paths;
using Microsoft.Data.Sqlite;

namespace FlowNote.Infrastructure.Attachments;

public sealed class AttachmentPipeline
{
    private readonly AppStoragePaths _paths;
    private readonly IClock _clock;

    public AttachmentPipeline(AppStoragePaths paths, IClock clock)
    {
        _paths = paths;
        _clock = clock;
    }

    public PendingAttachment StageIncoming(PendingAttachment pending, int currentCount)
    {
        if (pending.SourcePath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(pending.OriginalName))
        {
            throw new ValidationException("파일 이름에 경로를 넣을 수 없습니다.");
        }

        if (!File.Exists(pending.SourcePath))
        {
            throw new ValidationException("첨부할 파일을 찾을 수 없습니다.");
        }

        var size = new FileInfo(pending.SourcePath).Length;
        AttachmentRules.ValidateFile(pending.OriginalName, size, currentCount);

        var alreadyStaged = Path.GetFullPath(pending.SourcePath)
            .StartsWith(Path.GetFullPath(_paths.StagingDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (alreadyStaged)
        {
            return new PendingAttachment
            {
                OriginalName = pending.OriginalName,
                SourcePath = pending.SourcePath,
                MediaType = pending.MediaType,
                ByteSize = size
            };
        }

        var stagedName = "draft-" + Guid.NewGuid().ToString("N") + SafeExtension(pending.OriginalName);
        var stagedPath = Path.Combine(_paths.StagingDirectory, stagedName);
        File.Copy(pending.SourcePath, stagedPath, overwrite: false);
        return new PendingAttachment
        {
            OriginalName = pending.OriginalName,
            SourcePath = stagedPath,
            MediaType = pending.MediaType,
            ByteSize = size
        };
    }

    public PreparedAttachment Prepare(PendingAttachment pending, int currentCount)
    {
        if (pending.SourcePath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(pending.OriginalName))
        {
            throw new ValidationException("파일 이름에 경로를 넣을 수 없습니다.");
        }

        if (!File.Exists(pending.SourcePath))
        {
            throw new ValidationException("첨부할 파일을 찾을 수 없습니다.");
        }

        var size = new FileInfo(pending.SourcePath).Length;
        AttachmentRules.ValidateFile(pending.OriginalName, size, currentCount);

        var id = Guid.NewGuid().ToString("D");
        var storedName = id + SafeExtension(pending.OriginalName);
        var stagingPath = Path.Combine(_paths.StagingDirectory, storedName);
        File.Copy(pending.SourcePath, stagingPath, overwrite: false);

        string sha;
        using (var stream = File.OpenRead(stagingPath))
        {
            sha = Convert.ToHexString(SHA256.HashData(stream));
        }

        var relative = Path.Combine("attachments", storedName).Replace('\\', '/');
        var finalPath = Path.Combine(_paths.AttachmentsDirectory, storedName);
        File.Move(stagingPath, finalPath, overwrite: false);

        return new PreparedAttachment(
            id,
            pending.OriginalName,
            relative,
            pending.MediaType,
            size,
            sha,
            UtcInstant.TruncateToMilliseconds(_clock.UtcNow));
    }

    public static void Insert(SqliteConnection connection, SqliteTransaction transaction, string entryId, IReadOnlyList<PreparedAttachment> attachments)
    {
        for (var i = 0; i < attachments.Count; i++)
        {
            var item = attachments[i];
            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO attachments (id, original_name, stored_relative_path, media_type, byte_size, sha256, created_at_utc)
                    VALUES ($id, $name, $path, $media, $size, $sha, $created);
                    """;
                insert.Parameters.AddWithValue("$id", item.Id);
                insert.Parameters.AddWithValue("$name", item.OriginalName);
                insert.Parameters.AddWithValue("$path", item.StoredRelativePath);
                insert.Parameters.AddWithValue("$media", item.MediaType);
                insert.Parameters.AddWithValue("$size", item.ByteSize);
                insert.Parameters.AddWithValue("$sha", item.Sha256);
                insert.Parameters.AddWithValue("$created", UtcInstant.ToStorage(item.CreatedAtUtc));
                insert.ExecuteNonQuery();
            }

            using (var link = connection.CreateCommand())
            {
                link.Transaction = transaction;
                link.CommandText = "INSERT INTO entry_attachments (entry_id, attachment_id, sort_order) VALUES ($entry, $attachment, $order);";
                link.Parameters.AddWithValue("$entry", entryId);
                link.Parameters.AddWithValue("$attachment", item.Id);
                link.Parameters.AddWithValue("$order", i);
                link.ExecuteNonQuery();
            }
        }
    }

    public IReadOnlyList<StoredAttachment> ListForEntry(SqliteConnection connection, string entryId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.id, a.original_name, a.stored_relative_path, a.media_type, a.byte_size, a.sha256, a.created_at_utc, a.thumbnail_relative_path
            FROM attachments a
            INNER JOIN entry_attachments ea ON ea.attachment_id = a.id
            WHERE ea.entry_id = $entry
            ORDER BY ea.sort_order ASC;
            """;
        command.Parameters.AddWithValue("$entry", entryId);
        using var reader = command.ExecuteReader();
        var list = new List<StoredAttachment>();
        while (reader.Read())
        {
            list.Add(new StoredAttachment
            {
                Id = reader.GetString(0),
                OriginalName = reader.GetString(1),
                StoredRelativePath = reader.GetString(2),
                MediaType = reader.GetString(3),
                ByteSize = reader.GetInt64(4),
                Sha256 = reader.GetString(5),
                CreatedAtUtc = UtcInstant.Parse(reader.GetString(6)),
                ThumbnailRelativePath = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return list;
    }

    public string ResolveFullPath(StoredAttachment attachment)
    {
        var relative = attachment.StoredRelativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(_paths.Root, relative));
    }

    public void TryDeleteUnreferenced(PreparedAttachment prepared, IReadOnlySet<string> linkedIds)
    {
        if (linkedIds.Contains(prepared.Id))
        {
            return;
        }

        var relative = prepared.StoredRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_paths.Root, relative));
        if (File.Exists(full))
        {
            File.Delete(full);
        }
    }

    private static string SafeExtension(string originalName)
    {
        var extension = Path.GetExtension(originalName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
        {
            return ".bin";
        }

        return extension;
    }
}

public sealed record PreparedAttachment(
    string Id,
    string OriginalName,
    string StoredRelativePath,
    string MediaType,
    long ByteSize,
    string Sha256,
    DateTimeOffset CreatedAtUtc);
