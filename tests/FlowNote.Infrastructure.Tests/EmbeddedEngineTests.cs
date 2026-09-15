using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FlowNote.Core.Assist;
using FlowNote.Infrastructure.Assist.Embedded;
using FlowNote.Infrastructure.Paths;

namespace FlowNote.Infrastructure.Tests;

public sealed class EmbeddedEngineTests
{
    [Fact]
    public void Command_line_quotes_spaces_and_quotes()
    {
        var line = Win32CommandLine.Build(@"C:\Program Files\app.exe", ["a b", @"say ""hi"""]);
        Assert.Contains("\"C:\\Program Files\\app.exe\"", line, StringComparison.Ordinal);
        Assert.Contains("\"a b\"", line, StringComparison.Ordinal);
        Assert.Contains("\\\"", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Unresolved_lock_is_not_ready()
    {
        using var temp = new TempDatabase();
        var verifier = new AssetVerifier(temp.Root, temp.Database.Paths);
        var check = verifier.Check();
        Assert.False(check.Ready);
        Assert.Equal("lock-unresolved", check.Reason);
    }

    [Fact]
    public void OpenAi_parser_rejects_tool_calls_and_fences()
    {
        var request = new InferenceRequest
        {
            EntryId = "e1",
            NoteText = "원문",
            Candidates = [],
            EntryRevision = "rev-1",
            CorrectionRevision = 0,
            PolicyRevision = 1
        };
        var tools = """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"{}","tool_calls":[{"id":"1"}]}}]}""";
        Assert.Equal("tool-calls-forbidden", OpenAiChatParser.Parse(tools, request).ErrorCode);
        var fenced = """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"```json\n{}\n```"}}]}""";
        Assert.Equal("invalid-json", OpenAiChatParser.Parse(fenced, request).ErrorCode);
    }

    [Fact]
    public async Task Http_client_rejects_non_loopback()
    {
        await Task.Yield();
        Assert.Throws<InvalidOperationException>(() => new LocalLlamaHttpClient(new Uri("http://example.com/")));
        Assert.Throws<InvalidOperationException>(() => new LocalLlamaHttpClient(new Uri("http://localhost:9/")));
        Assert.Throws<InvalidOperationException>(() => new LocalLlamaHttpClient(new Uri("http://127.0.0.2:9/")));
        Assert.False(OwnedEnginePath.IsAllowed(@"C:\app", @"C:\Windows\System32\ping.exe"));
        Assert.False(OwnedEnginePath.IsAllowed(@"C:\app", @"C:\app\ai-runtime\cpu\b10964\ggml-rpc-server.exe"));
        Assert.True(OwnedEnginePath.IsAllowed(@"C:\app", @"C:\app\ai-runtime\cpu\b10964\llama-server.exe"));
    }

    [Fact]
    public async Task Fake_openai_server_maps_content()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer);
            var body = """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"{\"entry_id\":\"e1\",\"decision\":\"abstain\",\"primary\":null,\"mentions\":[]}"}}]}""";
            var response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\nConnection: close\r\n\r\n" + body;
            var bytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(bytes);
        });
        using var http = new LocalLlamaHttpClient(new Uri($"http://127.0.0.1:{port}/"));
        http.SetApiKey("test-key");
        var raw = await http.ChatCompletionsAsync(new { model = "x" }, CancellationToken.None);
        var parsed = OpenAiChatParser.Parse(raw, new InferenceRequest
        {
            EntryId = "e1",
            NoteText = "원문",
            Candidates = [],
            EntryRevision = "rev-1",
            CorrectionRevision = 0,
            PolicyRevision = 1
        });
        Assert.Equal(AssistDecision.Abstain, parsed.Decision);
        listener.Stop();
        await server;
    }

    [Fact]
    public void Job_object_starts_and_kills_child()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var ping = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe");
        if (!File.Exists(ping))
        {
            return;
        }

        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            env[entry.Key!.ToString()!] = entry.Value?.ToString() ?? "";
        }

        using var child = NativeJobProcess.Start(ping, ["-n", "1", "127.0.0.1"], env);
        Assert.True(child.ProcessId > 0);
        Assert.True(child.WaitForExit(TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void Parser_rejects_non_stop_and_thinking()
    {
        var request = new InferenceRequest
        {
            EntryId = "e1",
            NoteText = "원문",
            Candidates = [],
            EntryRevision = "rev-1",
            CorrectionRevision = 0,
            PolicyRevision = 1
        };
        var length = """{"choices":[{"finish_reason":"length","message":{"role":"assistant","content":"{}"}}]}""";
        Assert.Equal("finish-reason", OpenAiChatParser.Parse(length, request).ErrorCode);
        var thinking = """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"{}","reasoning_content":"think"}}]}""";
        Assert.Equal("thinking-output", OpenAiChatParser.Parse(thinking, request).ErrorCode);
        var many = """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"{}"}},{"finish_reason":"stop","message":{"role":"assistant","content":"{}"}}]}""";
        Assert.Equal("choices-count", OpenAiChatParser.Parse(many, request).ErrorCode);
    }

    [Fact]
    public void Tampered_engine_file_is_not_ready()
    {
        using var temp = new TempDatabase();
        var engineDir = Path.Combine(temp.Root, "ai-runtime", "cpu", "b10964");
        Directory.CreateDirectory(engineDir);
        var exe = Path.Combine(engineDir, "llama-server.exe");
        File.WriteAllText(exe, "not-an-engine");
        var lockDoc = new
        {
            provider = "embedded-llama.cpp",
            lockVersion = 1,
            engine = new
            {
                repo = "ggml-org/llama.cpp",
                releaseTag = "b10964",
                commit = "b29c606e28a01b1bc8c1351026a0fa6e616bf6c4",
                archiveSha256 = "917f39c076402c421224824607397af20f53625a60defc20e8dd22446bf4c5d7",
                files = new[]
                {
                    new { relativePath = "llama-server.exe", sha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", bytes = 13 }
                }
            },
            model = new
            {
                repo = "Qwen/Qwen3-4B-GGUF",
                revision = "bc640142c66e1fdd12af0bd68f40445458f3869b",
                sha256 = "7485fe6f11af29433bc51cab58009521f205840f5b4ae3a32fa7f92e8534fdf5"
            }
        };
        Directory.CreateDirectory(Path.Combine(temp.Root, "ai-runtime"));
        File.WriteAllText(
            Path.Combine(temp.Root, "ai-runtime", "engine-lock.json"),
            JsonSerializer.Serialize(lockDoc));
        var verifier = new AssetVerifier(temp.Root, temp.Database.Paths);
        var check = verifier.Check();
        Assert.False(check.Ready);
        Assert.Equal("engine-hash-mismatch", check.Reason);
    }

    [Fact]
    public void Tcp_listener_pid_matches_this_process()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Assert.True(TcpPortOwner.TryGetListenerPid(port, out var pid));
            Assert.Equal(Environment.ProcessId, pid);
        }
        finally
        {
            listener.Stop();
        }
    }
}
