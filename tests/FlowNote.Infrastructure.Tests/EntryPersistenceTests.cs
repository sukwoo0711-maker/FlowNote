using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Time;
using FlowNote.Infrastructure.Paths;

namespace FlowNote.Infrastructure.Tests;

public sealed class EntryPersistenceTests
{
    [Fact]
    public async Task Save_then_reopen_restores_korean_text_and_times()
    {
        using var temp = new TempDatabase();
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "req-1",
            Body = "상태 초기화 순서를 확인할 필요가 있음"
        });

        var listed = await temp.Reopen().Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));

        var found = Assert.Single(listed);
        Assert.Equal(saved.Id, found.Id);
        Assert.Equal("상태 초기화 순서를 확인할 필요가 있음", found.Body);
        Assert.Equal(saved.RecordedAtUtc, found.RecordedAtUtc);
        Assert.Equal(EntryKind.Note, found.Kind);
    }

    [Fact]
    public async Task Same_request_id_does_not_create_a_second_entry()
    {
        using var temp = new TempDatabase();
        var first = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "same", Body = "첫 저장" });
        var second = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "same", Body = "재시도" });
        var listed = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("첫 저장", second.Body);
        Assert.Single(listed);
    }

    [Fact]
    public async Task Korea_midnight_boundary_splits_utc_same_day()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 14, 14, 59, 0, TimeSpan.Zero));
        using var temp = new TempDatabase(clock);
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "before", Body = "23:59" });

        clock.UtcNow = new DateTimeOffset(2026, 9, 14, 15, 1, 0, TimeSpan.Zero);
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "after", Body = "00:01" });

        var sept14 = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        var sept15 = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 15));

        Assert.Equal("23:59", Assert.Single(sept14).Body);
        Assert.Equal("00:01", Assert.Single(sept15).Body);
    }

    [Fact]
    public async Task Twenty_entries_with_same_occurred_at_keep_seq_order()
    {
        using var temp = new TempDatabase();
        var occurred = new DateTimeOffset(2026, 9, 14, 1, 42, 0, TimeSpan.Zero);
        var ids = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
            {
                RequestId = $"same-minute-{i}",
                Body = $"기록 {i}",
                OccurredAtUtc = occurred
            });
            ids.Add(saved.Id);
        }

        var first = (await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14))).Select(static item => item.Id).ToArray();
        var second = (await temp.Reopen().Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14))).Select(static item => item.Id).ToArray();

        Assert.Equal(ids, first);
        Assert.Equal(ids, second);
        Assert.Equal(Enumerable.Range(0, 20).Select(static i => $"기록 {i}"), (await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14))).Select(static item => item.Body));
    }

    [Fact]
    public async Task User_occurred_time_does_not_change_recorded_at()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 14, 1, 24, 0, TimeSpan.Zero));
        using var temp = new TempDatabase(clock);
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "late",
            Body = "늦게 적은 메모",
            OccurredAtUtc = new DateTimeOffset(2026, 9, 14, 1, 0, 0, TimeSpan.Zero)
        });

        Assert.Equal(UtcInstant.Parse("2026-09-14T01:24:00.000Z"), saved.RecordedAtUtc);
        Assert.Equal(UtcInstant.Parse("2026-09-14T01:00:00.000Z"), saved.OccurredAtUtc);
        Assert.Equal(OccurredTimeSource.User, saved.OccurredTimeSource);

        clock.UtcNow = clock.UtcNow.AddMinutes(10);
        var listed = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        Assert.Equal(saved.RecordedAtUtc, Assert.Single(listed).RecordedAtUtc);
    }

    [Fact]
    public async Task Parameter_binding_survives_quotes_and_sql_tokens()
    {
        using var temp = new TempDatabase();
        var body = "펌프'; DROP TABLE entries; -- % _";
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "inject", Body = body });
        var listed = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        Assert.Equal(body, Assert.Single(listed).Body);
    }

    [Fact]
    public async Task Whitespace_only_note_is_rejected()
    {
        using var temp = new TempDatabase();
        await Assert.ThrowsAsync<ValidationException>(() => temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "blank",
            Body = "   \n"
        }));
        Assert.Empty(await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14)));
    }

    [Fact]
    public async Task Attachment_only_note_without_text_is_allowed_by_rule()
    {
        using var temp = new TempDatabase();
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "image-only",
            Body = "  ",
            HasAttachments = true
        });
        Assert.Equal("  ", saved.Body);
    }

    [Fact]
    public async Task Search_finds_korean_text_and_escapes_like_wildcards()
    {
        using var temp = new TempDatabase();
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "k", Body = "상태 초기화 순서를 확인할 필요가 있음" });
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "w", Body = "펌프 100% 부하_테스트" });

        var korean = temp.Database.Entries.Search("초기화");
        Assert.Equal("상태 초기화 순서를 확인할 필요가 있음", Assert.Single(korean).Body);

        var percent = temp.Database.Entries.Search("%");
        Assert.Equal("펌프 100% 부하_테스트", Assert.Single(percent).Body);

        Assert.Empty(temp.Database.Entries.Search("없는검색어xyz"));
    }

    [Fact]
    public void Live_and_demo_paths_are_separate_under_local_app_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "FlowNotePath", Guid.NewGuid().ToString("N"));
        try
        {
            var live = AppStoragePaths.Create(AppStorageMode.Live, root);
            var demo = AppStoragePaths.Create(AppStorageMode.Demo, root);
            Assert.NotEqual(live.DatabasePath, demo.DatabasePath);
            Assert.Contains($"{Path.DirectorySeparatorChar}live", live.Root, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"{Path.DirectorySeparatorChar}demo", demo.Root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("src", live.Root, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
