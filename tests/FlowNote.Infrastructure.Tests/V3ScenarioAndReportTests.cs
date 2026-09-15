using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Core.Time;
using FlowNote.Infrastructure.Demo;
using FlowNote.Infrastructure.Reports;

namespace FlowNote.Infrastructure.Tests;

public sealed class V3ScenarioAndReportTests
{
    [Fact]
    public async Task Scenario_completes_once_and_report_excludes_private()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero));
        using var temp = new TempDatabase(clock);
        var fixtureDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures", "v3"));
        var ids = await V3ScenarioSeeder.SeedAsync(temp.Database, value => clock.UtcNow = value, fixtureDir);

        Assert.Equal(WorkItemStatus.Completed, (await temp.Database.WorkItems.GetAsync(ids.WorkA))!.Status);
        Assert.Equal(WorkItemStatus.Open, (await temp.Database.WorkItems.GetAsync(ids.WorkB))!.Status);
        Assert.Null((await temp.Database.WorkItems.GetAsync(ids.WorkA))!.NextActionText);
        Assert.True(ids.CompleteAlreadyApplied);
        var day = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 15));
        Assert.Equal(1, day.Count(static item => item.Kind == EntryKind.TaskCompleted && item.WorkItemId != null));

        var context = temp.Database.WorkContext.Get(ids.WorkA, 1);
        Assert.Equal(ids.NoteA3, context.LatestNote?.Id);
        Assert.Contains(context.RelatedAttachments, item => item.Attachment.Id == ids.ImageAttachment);

        var catalog = temp.Database.Reports.ForDate(new DateOnly(2026, 9, 15), null, [ids.NoteA2]);
        var snapshot = ReportBuilder.Build(new ReportBuildRequest
        {
            ReportDate = new DateOnly(2026, 9, 15),
            TimeZoneDisplay = "Asia/Seoul",
            SelectedIds = [ids.NoteA3, ids.CompleteEvent!],
            SelectedAttachmentIds = [],
            Catalog = catalog
        }, DateTimeOffset.UtcNow);
        var withEvidence = ReportBuilder.Build(new ReportBuildRequest
        {
            ReportDate = new DateOnly(2026, 9, 15),
            TimeZoneDisplay = "Asia/Seoul",
            SelectedIds = [ids.NoteA3, ids.CompleteEvent!, ids.NoteA2],
            SelectedAttachmentIds = [ids.ImageAttachment],
            Catalog = catalog
        }, DateTimeOffset.UtcNow);

        Assert.DoesNotContain("PRIVATE-DO-NOT-EXPORT", snapshot.Markdown);
        Assert.Contains("2026-09-14", withEvidence.Markdown);
        Assert.DoesNotContain("sample-uart.log", withEvidence.Markdown);

        var zipPath = Path.Combine(temp.Root, "report.zip");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in withEvidence.Files)
        {
            var stored = temp.Database.Entries.ListAttachments(file.EntryId).First(item => item.Id == file.AttachmentId);
            paths[file.AttachmentId] = temp.Database.Attachments.ResolveFullPath(stored);
        }

        temp.Database.ReportZip.Write(withEvidence, zipPath, paths);
        var delivery = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "v3"));
        Directory.CreateDirectory(delivery);
        File.Copy(zipPath, Path.Combine(delivery, "selected-report.zip"), overwrite: true);
        using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
        var names = zip.Entries.Select(static item => item.FullName).ToArray();
        Assert.Contains("report.md", names);
        Assert.Contains("manifest.json", names);
        Assert.Contains(names, static name => name.StartsWith("attachments/", StringComparison.Ordinal));
        var extracted = zip.GetEntry("report.md")!;
        using var reader = new StreamReader(extracted.Open());
        var md = reader.ReadToEnd();
        Assert.DoesNotContain("PRIVATE-DO-NOT-EXPORT", md);
        Assert.DoesNotContain("sample-uart.log", md);
        Assert.DoesNotContain(temp.Root, md, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Backup_restores_next_action()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "백업 대상" });
        await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na",
            ExpectedVersion = created.WorkItem.Version,
            Text = "복원되어야 함",
            Reason = NextActionChangeReason.Set
        });
        var backupDir = Path.Combine(temp.Root, "backup");
        temp.Database.Backups.BackupTo(backupDir);
        temp.Database.Dispose();

        var restorePaths = FlowNote.Infrastructure.Paths.AppStoragePaths.Create(
            FlowNote.Infrastructure.Paths.AppStorageMode.Live,
            Path.Combine(temp.Root, "restored-appdata"));
        temp.Database.Backups.RestoreFrom(backupDir, restorePaths.Root);
        using var restored = new FlowNote.Infrastructure.FlowNoteDatabase(restorePaths, temp.Clock, temp.TimeZone);
        var item = await restored.WorkItems.GetAsync(created.WorkItem.Id);
        Assert.Equal("복원되어야 함", item!.NextActionText);
    }

    [Fact]
    public async Task Migration_2_preserves_existing_notes()
    {
        using var temp = new TempDatabase();
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "keep", Body = "기존 메모" });
        temp.Reopen();
        var listed = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        Assert.Equal("기존 메모", Assert.Single(listed).Body);
    }
}
