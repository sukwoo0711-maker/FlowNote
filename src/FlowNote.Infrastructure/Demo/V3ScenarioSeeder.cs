using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Core.Time;

namespace FlowNote.Infrastructure.Demo;

public static class V3ScenarioSeeder
{
    public const string SettingKey = "demo.v3.scenario";
    public const string SettingValue = "1";

    public static async Task EnsureAsync(FlowNoteDatabase database, Action<DateTimeOffset> setClock, string? fixtureDirectory)
    {
        if (database.Settings.Get(SettingKey) == SettingValue)
        {
            return;
        }

        await SeedAsync(database, setClock, fixtureDirectory);
        database.Settings.Set(SettingKey, SettingValue);
    }

    public static async Task<V3ScenarioIds> SeedAsync(FlowNoteDatabase database, Action<DateTimeOffset> setClock, string? fixtureDirectory)
    {
        var ids = new V3ScenarioIds();
        var png = Path.Combine(database.Paths.StagingDirectory, "synthetic-debug.png");
        SyntheticPng.Write(png);
        var logPath = CopyOrWrite(fixtureDirectory, "sample-uart.log", database.Paths.StagingDirectory, "sample-uart.log",
            "UART ready\nlast=SYNTHETIC-UART-OK\n");
        var privatePath = CopyOrWrite(fixtureDirectory, "PRIVATE-DO-NOT-EXPORT.log", database.Paths.StagingDirectory,
            "PRIVATE-DO-NOT-EXPORT.log", "PRIVATE-DO-NOT-EXPORT synthetic token\n");

        setClock(Offset("2026-09-14T09:00:00+09:00"));
        var workA = await database.WorkItems.CreateAsync(new CreateWorkItemRequest
        {
            RequestId = "seed-create-A",
            Title = "취소 후 재시작 오류 확인",
            IssueKey = "DEV-1234"
        });
        ids.WorkA = workA.WorkItem.Id;

        setClock(Offset("2026-09-14T09:12:00+09:00"));
        var n1 = await database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "seed-N-A1",
            Body = "조건 B에서만 재현됨. UART 마지막 출력 확인 필요.",
            WorkItemId = ids.WorkA,
            OccurredAtUtc = Offset("2026-09-14T09:12:00+09:00")
        });
        ids.NoteA1 = n1.Id;

        setClock(Offset("2026-09-14T09:18:00+09:00"));
        var n2 = await database.Entries.SaveNoteAsync(
            new SaveNoteRequest
            {
                RequestId = "seed-N-A2",
                Body = "초기화 직전의 화면과 UART 로그를 남겼다.",
                WorkItemId = ids.WorkA,
                OccurredAtUtc = Offset("2026-09-14T09:18:00+09:00"),
                HasAttachments = true
            },
            [
                new PendingAttachment { OriginalName = "synthetic-debug.png", SourcePath = png, MediaType = "image/png", ByteSize = new FileInfo(png).Length },
                new PendingAttachment { OriginalName = "sample-uart.log", SourcePath = logPath, MediaType = "text/plain", ByteSize = new FileInfo(logPath).Length }
            ]);
        ids.NoteA2 = n2.Id;
        var attachments = database.Entries.ListAttachments(n2.Id);
        ids.ImageAttachment = attachments.First(static item => item.OriginalName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).Id;
        ids.LogAttachment = attachments.First(static item => item.OriginalName.EndsWith(".log", StringComparison.OrdinalIgnoreCase)).Id;

        setClock(Offset("2026-09-14T09:25:00+09:00"));
        await database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = ids.WorkA,
            RequestId = "seed-NA-A1",
            ExpectedVersion = (await database.WorkItems.GetAsync(ids.WorkA))!.Version,
            Text = "펌프 OFF와 상태 초기화 순서를 비교한다.",
            SourceEntryId = ids.NoteA1,
            SourceRevision = ContentRevision.Sha256Hex(n1.Body),
            Reason = NextActionChangeReason.Set
        });

        setClock(Offset("2026-09-14T10:00:00+09:00"));
        var workB = await database.WorkItems.CreateAsync(new CreateWorkItemRequest
        {
            RequestId = "seed-create-B",
            Title = "다른 시험 조건 정리"
        });
        ids.WorkB = workB.WorkItem.Id;

        setClock(Offset("2026-09-15T09:05:00+09:00"));
        var privateNote = await database.Entries.SaveNoteAsync(
            new SaveNoteRequest
            {
                RequestId = "seed-N-PRIVATE",
                Body = "PRIVATE-DO-NOT-EXPORT — 합성 제외 검수용. 외부 보고에 담지 않는다.",
                WorkItemId = ids.WorkB,
                OccurredAtUtc = Offset("2026-09-15T09:05:00+09:00"),
                HasAttachments = true
            },
            [
                new PendingAttachment
                {
                    OriginalName = "PRIVATE-DO-NOT-EXPORT.log",
                    SourcePath = privatePath,
                    MediaType = "text/plain",
                    ByteSize = new FileInfo(privatePath).Length
                }
            ]);
        ids.NotePrivate = privateNote.Id;
        ids.PrivateAttachment = database.Entries.ListAttachments(privateNote.Id)[0].Id;

        setClock(Offset("2026-09-15T09:10:00+09:00"));
        var n3 = await database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "seed-N-A3",
            Body = "조건 B의 추가 재현 시험을 마쳤다. 초기화 순서와 로그를 대조했다.",
            WorkItemId = ids.WorkA,
            OccurredAtUtc = Offset("2026-09-15T09:10:00+09:00")
        });
        ids.NoteA3 = n3.Id;

        setClock(Offset("2026-09-15T09:20:00+09:00"));
        var currentA = await database.WorkItems.GetAsync(ids.WorkA);
        var completed = await database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = ids.WorkA,
            RequestId = "REQ-COMPLETE-A",
            ExpectedVersion = currentA!.Version
        });
        ids.CompleteEvent = completed.Event?.Id;

        var retry = await database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = ids.WorkA,
            RequestId = "REQ-COMPLETE-A",
            ExpectedVersion = currentA.Version
        });
        ids.CompleteAlreadyApplied = retry.AlreadyApplied;
        return ids;
    }

    private static string CopyOrWrite(string? fixtureDirectory, string name, string destDir, string destName, string fallback)
    {
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, destName);
        var source = fixtureDirectory is null ? null : Path.Combine(fixtureDirectory, name);
        if (source is not null && File.Exists(source))
        {
            File.Copy(source, dest, overwrite: true);
        }
        else
        {
            File.WriteAllText(dest, fallback);
        }

        return dest;
    }

    private static DateTimeOffset Offset(string value) => DateTimeOffset.Parse(value);
}

public sealed class V3ScenarioIds
{
    public string WorkA { get; set; } = "";
    public string WorkB { get; set; } = "";
    public string NoteA1 { get; set; } = "";
    public string NoteA2 { get; set; } = "";
    public string NoteA3 { get; set; } = "";
    public string NotePrivate { get; set; } = "";
    public string? CompleteEvent { get; set; }
    public string ImageAttachment { get; set; } = "";
    public string LogAttachment { get; set; } = "";
    public string PrivateAttachment { get; set; } = "";
    public bool CompleteAlreadyApplied { get; set; }
}
