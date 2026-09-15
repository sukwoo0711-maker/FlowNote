using System.Security.Cryptography;
using System.Text.Json;
using FlowNote.Core.Assist;
using FlowNote.Infrastructure.Paths;

namespace FlowNote.Infrastructure.Assist.Embedded;

public sealed class EngineLockDocument
{
    public string Provider { get; set; } = AssistVersions.EmbeddedProvider;
    public int LockVersion { get; set; } = 1;
    public EngineLockSection Engine { get; set; } = new();
    public ModelLockSection Model { get; set; } = new();
    public ProtocolLockSection Protocol { get; set; } = new();
}

public sealed class EngineLockSection
{
    public string Repo { get; set; } = "ggml-org/llama.cpp";
    public string? ReleaseTag { get; set; }
    public string? Commit { get; set; }
    public string? AssetUrl { get; set; }
    public string? ArchiveSha256 { get; set; }
    public long? ArchiveBytes { get; set; }
    public string Backend { get; set; } = "cpu";
    public List<LockedFile> Files { get; set; } = [];
}

public sealed class ModelLockSection
{
    public string Repo { get; set; } = "Qwen/Qwen3-4B-GGUF";
    public string? Revision { get; set; }
    public string FileName { get; set; } = AssistVersions.ModelFileName;
    public string Quantization { get; set; } = "Q4_K_M";
    public string? Sha256 { get; set; }
    public long? Bytes { get; set; }
}

public sealed class ProtocolLockSection
{
    public string Endpoint { get; set; } = "/v1/chat/completions";
    public string AdapterVersion { get; set; } = "v41-llama-1";
}

public sealed class LockedFile
{
    public string RelativePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Bytes { get; set; }
}

public sealed class AssetCheck
{
    public bool EngineReady { get; init; }
    public bool ModelReady { get; init; }
    public string? Reason { get; init; }
    public string? EngineExe { get; init; }
    public string? ModelPath { get; init; }
    public string? Fingerprint { get; init; }
    public EngineLockDocument? Lock { get; init; }

    public bool Ready => EngineReady && ModelReady && EngineExe is not null && ModelPath is not null;
}

public sealed class AssetVerifier
{
    private readonly string _appBase;
    private readonly AppStoragePaths _paths;
    private AssetCheck? _cached;
    private string? _cachedIdentity;

    public AssetVerifier(string appBase, AppStoragePaths paths)
    {
        _appBase = Path.GetFullPath(appBase);
        _paths = paths;
    }

    public string AppBase => _appBase;

    public AssetCheck Check(bool force = false)
    {
        var lockPath = Path.Combine(_appBase, "ai-runtime", "engine-lock.json");
        EngineLockDocument? document = null;
        if (File.Exists(lockPath))
        {
            document = JsonSerializer.Deserialize<EngineLockDocument>(File.ReadAllText(lockPath), JsonOptions());
        }

        document ??= DefaultLock();
        if (IsUnresolved(document))
        {
            return new AssetCheck { Reason = "lock-unresolved", Lock = document };
        }

        var engineDir = Path.Combine(_appBase, "ai-runtime", "cpu", document.Engine.ReleaseTag ?? AssistVersions.EngineReleaseTag);
        var exe = Path.Combine(engineDir, "llama-server.exe");
        if (!File.Exists(exe))
        {
            return new AssetCheck { Reason = "engine-missing", Lock = document };
        }

        if (!VerifyFiles(engineDir, document.Engine.Files, out var fileReason))
        {
            return new AssetCheck { Reason = fileReason, Lock = document };
        }

        var model = ResolveModel(document);
        if (model is null)
        {
            return new AssetCheck
            {
                EngineReady = true,
                EngineExe = exe,
                Reason = "model-missing",
                Lock = document
            };
        }

        var identity = $"{exe}|{new FileInfo(exe).Length}|{new FileInfo(exe).LastWriteTimeUtc:O}|{model}|{new FileInfo(model).Length}|{new FileInfo(model).LastWriteTimeUtc:O}";
        if (!force && _cached is not null && _cachedIdentity == identity)
        {
            return _cached;
        }

        var modelHash = HashFile(model);
        if (!string.Equals(modelHash, document.Model.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return new AssetCheck
            {
                EngineReady = true,
                EngineExe = exe,
                Reason = "model-hash-mismatch",
                Lock = document
            };
        }

        var fingerprint = ContentRevisionLike($"{document.Engine.ReleaseTag}|{document.Engine.Commit}|{modelHash}|{document.Protocol.AdapterVersion}");
        var check = new AssetCheck
        {
            EngineReady = true,
            ModelReady = true,
            EngineExe = exe,
            ModelPath = model,
            Fingerprint = fingerprint,
            Lock = document
        };
        _cached = check;
        _cachedIdentity = identity;
        return check;
    }

    public async Task<AssetCheck> ImportModelAsync(string sourcePath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var check = Check(force: true);
        var expected = check.Lock?.Model.Sha256;
        if (string.IsNullOrWhiteSpace(expected) || expected.Contains("null", StringComparison.OrdinalIgnoreCase))
        {
            return new AssetCheck { Reason = "lock-unresolved", Lock = check.Lock };
        }

        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            return new AssetCheck { Reason = "import-source-missing", Lock = check.Lock };
        }

        Directory.CreateDirectory(_paths.SharedModelsDirectory);
        var staging = Path.Combine(_paths.StagingDirectory, "model-import-" + Guid.NewGuid().ToString("N") + ".gguf");
        try
        {
            await CopyHashedAsync(source, staging, expected, progress, cancellationToken);
            var destDir = Path.Combine(_paths.SharedModelsDirectory, expected.ToLowerInvariant());
            Directory.CreateDirectory(destDir);
            var dest = Path.Combine(destDir, AssistVersions.ModelFileName);
            if (File.Exists(dest))
            {
                File.Delete(dest);
            }

            File.Move(staging, dest);
            return Check(force: true);
        }
        catch (OperationCanceledException)
        {
            TryDelete(staging);
            return new AssetCheck { Reason = "import-cancelled", Lock = check.Lock };
        }
        catch (IOException)
        {
            TryDelete(staging);
            return new AssetCheck { Reason = "import-failed", Lock = check.Lock };
        }
        catch (InvalidDataException)
        {
            TryDelete(staging);
            return new AssetCheck { Reason = "model-hash-mismatch", Lock = check.Lock };
        }
    }

    private string? ResolveModel(EngineLockDocument document)
    {
        var packaged = Path.Combine(_appBase, "ai-models", AssistVersions.ModelFileName);
        if (File.Exists(packaged))
        {
            return packaged;
        }

        if (!string.IsNullOrWhiteSpace(document.Model.Sha256))
        {
            var imported = Path.Combine(_paths.SharedModelsDirectory, document.Model.Sha256.ToLowerInvariant(), AssistVersions.ModelFileName);
            if (File.Exists(imported))
            {
                return imported;
            }
        }

        return null;
    }

    private static bool VerifyFiles(string engineDir, IReadOnlyList<LockedFile> files, out string? reason)
    {
        reason = null;
        if (files.Count == 0)
        {
            reason = "lock-unresolved";
            return false;
        }

        foreach (var file in files)
        {
            if (file.RelativePath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(file.RelativePath))
            {
                reason = "lock-path-escape";
                return false;
            }

            var full = Path.Combine(engineDir, file.RelativePath);
            if (!File.Exists(full))
            {
                reason = "engine-dll-missing";
                return false;
            }

            var info = new FileInfo(full);
            if (info.Length != file.Bytes || !string.Equals(HashFile(full), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                reason = "engine-hash-mismatch";
                return false;
            }
        }

        return true;
    }

    private static bool IsUnresolved(EngineLockDocument document)
    {
        static bool Bad(string? value)
            => string.IsNullOrWhiteSpace(value)
               || value.Contains("null", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "REPLACE", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "latest", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "main", StringComparison.OrdinalIgnoreCase)
               || value.All(static ch => ch == '0');

        return Bad(document.Engine.ReleaseTag)
               || Bad(document.Engine.Commit)
               || Bad(document.Engine.ArchiveSha256)
               || Bad(document.Model.Sha256)
               || document.Engine.Files.Count == 0;
    }

    private static EngineLockDocument DefaultLock()
        => new()
        {
            Engine = { ReleaseTag = AssistVersions.EngineReleaseTag }
        };

    private static JsonSerializerOptions JsonOptions()
        => new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static async Task CopyHashedAsync(
        string source,
        string destination,
        string expectedSha,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(source);
        await using var output = File.Create(destination);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 256];
        var total = input.Length;
        long copied = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hasher.AppendData(buffer.AsSpan(0, read));
            copied += read;
            if (total > 0)
            {
                progress?.Report(copied / (double)total);
            }
        }

        var actual = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(actual, expectedSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("model-hash-mismatch");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private static string ContentRevisionLike(string text)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
