using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Desktop.Controls;
using FlowNote.Desktop.Preview;
using FlowNote.Desktop.ViewModels;
using FlowNote.Desktop.Views;

namespace FlowNote.Desktop.Smoke;

internal sealed class SmokeHarness
{
    private readonly AppSession _session;
    private readonly MainViewModel _mainVm;
    private readonly FloatingViewModel _floatingVm;
    private readonly MainWindow _main;
    private readonly FloatingCapsuleWindow _floating;
    private readonly string _outputDir;
    private readonly List<string> _failed = [];
    private readonly List<string> _notes = [];

    public SmokeHarness(
        AppSession session,
        MainViewModel mainVm,
        FloatingViewModel floatingVm,
        MainWindow main,
        FloatingCapsuleWindow floating,
        string outputDir)
    {
        _session = session;
        _mainVm = mainVm;
        _floatingVm = floatingVm;
        _main = main;
        _floating = floating;
        _outputDir = outputDir;
    }

    public async Task<int> RunAsync()
    {
        Directory.CreateDirectory(_outputDir);
        await TraceAsync("start");
        _main.Show();
        _floating.Show();
        await WaitForLayoutAsync();
        if (_session.ShowOnboarding)
        {
            WindowCapture.Save(_main, Path.Combine(_outputDir, "00-onboarding.png"));
        }

        _session.DismissOnboarding();
        await WaitForLayoutAsync();
        AssertCapsuleChrome();
        await File.WriteAllTextAsync(Path.Combine(_outputDir, "capsule-metrics.txt"), _floating.ReadMetricsText());
        await MeasureWarmShowAsync();
        _session.Database.Settings.Set("floating.height", "500");
        _session.Database.Settings.Set("floating.width", "360");

        var todayDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _session.Database.DisplayTimeZone.TimeZone).DateTime);
        var existing = await _session.Database.Entries.ListForLocalDateAsync(todayDate);
        if (existing.Count == 0)
        {
            if (_floatingVm.CanSave)
            {
                _failed.Add("빈 입력에서 기록이 활성화되어 있습니다.");
            }

            await TraceAsync("empty-captures");
            WindowCapture.Save(_main, Path.Combine(_outputDir, "01-main-empty.png"));
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "02-floating-empty.png"));
            await TraceAsync("empty-captures-done");
            TryScreen(_main, "01-main-empty-screen.png");
            TryScreen(_floating, "02-floating-empty-screen.png");
            await CaptureIdleEvidenceAsync();

            var attachPath = Path.Combine(_session.Database.Paths.StagingDirectory, "smoke-note.txt");
            await File.WriteAllTextAsync(attachPath, "보드 로그 스모크");
            var pngPath = Path.Combine(_session.Database.Paths.StagingDirectory, "smoke-board.png");
            WritePreviewPng(pngPath);
            _floatingVm.Body = "보드 전원 시퀀스를 확인했다";
            _floatingVm.AddPending(new PendingAttachment
            {
                OriginalName = "smoke-note.txt",
                SourcePath = attachPath,
                MediaType = AttachmentRules.GuessMediaType(attachPath)
            });
            _floatingVm.AddPending(new PendingAttachment
            {
                OriginalName = "smoke-board.png",
                SourcePath = pngPath,
                MediaType = "image/png"
            });
            if (!_floatingVm.CanSave)
            {
                _failed.Add("본문과 첨부가 있는데 기록을 할 수 없습니다.");
            }

            await _floatingVm.SaveNoteAsync();
            await TraceAsync("saved-korean status=" + _floatingVm.StatusText + " err=" + _floatingVm.ErrorText);
            if (_floatingVm.RecentRows.All(static row => row.Title.Contains("보드 전원", StringComparison.Ordinal) == false))
            {
                _failed.Add("저장 직후 최근 목록에 본문이 없습니다.");
            }

            if (_floatingVm.PanelKind != CapsulePanelKind.Recent)
            {
                _failed.Add("첫 기록 뒤 기억판이 열리지 않았습니다.");
            }

            await WaitForLayoutAsync();
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "03-board-after-save.png"));
            TryScreen(_floating, "03-board-after-save-screen.png");
            if (_floatingVm.HasError || !_floatingVm.StatusText.Contains("기록됨", StringComparison.Ordinal))
            {
                _failed.Add("한글 메모 저장 실패: " + _floatingVm.StatusText + " / " + _floatingVm.ErrorText);
            }

            var imageOnly = Path.Combine(_session.Database.Paths.StagingDirectory, "smoke-image-only.png");
            WritePreviewPng(imageOnly);
            _floatingVm.Body = "";
            _floatingVm.AddPending(new PendingAttachment
            {
                OriginalName = "smoke-image-only.png",
                SourcePath = imageOnly,
                MediaType = "image/png"
            });
            await WaitForLayoutAsync();
            TryScreen(_floating, "06-image-draft.png");
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "06-image-draft-rtt.png"));
            if (!_floatingVm.CanSave)
            {
                _failed.Add("이미지 단독 첨부로 기록할 수 없습니다.");
            }

            await _floatingVm.SaveNoteAsync();
            await TraceAsync("saved-image-only");
            if (_floatingVm.RecentRows.All(static row => string.IsNullOrWhiteSpace(row.ImagePath)))
            {
                _failed.Add("사진만 저장한 기록이 최근 목록에 없습니다.");
            }

            var clipPng = Path.Combine(_session.Database.Paths.StagingDirectory, "clip-preview.png");
            WritePreviewPng(clipPng);
            var excelPaste = new DataObject();
            excelPaste.SetData("XML Spreadsheet", """
                <?xml version="1.0"?>
                <Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet">
                  <Worksheet ss:Name="Sheet1"/>
                </Workbook>
                """);
            excelPaste.SetData(DataFormats.UnicodeText, "A\tB\n1\t2");
            excelPaste.SetData("PNG", File.ReadAllBytes(clipPng));
            _floatingVm.Body = "남겨야 할 메모";
            if (!_floating.TryImportPaste(excelPaste))
            {
                _failed.Add("엑셀 원형 붙여넣기를 처리하지 못했습니다.");
            }

            if (!_floatingVm.PendingFiles.Any(static file => file.OriginalName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            {
                _failed.Add("엑셀 원형이 첨부로 들어오지 않았습니다.");
            }

            if (_floatingVm.PendingFiles.Any(static file => file.OriginalName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
            {
                _failed.Add("엑셀 붙여넣기가 그림만 남겼습니다.");
            }

            if (_floatingVm.Body != "남겨야 할 메모")
            {
                _failed.Add("엑셀 붙여넣기가 기존 입력을 바꿨습니다: " + _floatingVm.Body);
            }

            foreach (var pending in _floatingVm.PendingFiles.ToList())
            {
                _floatingVm.RemovePending(pending);
            }

            _floatingVm.Body = "";
            var imagePaste = new DataObject();
            imagePaste.SetData("PNG", File.ReadAllBytes(clipPng));
            if (!_floating.TryImportPaste(imagePaste))
            {
                _failed.Add("이미지 붙여넣기를 처리하지 못했습니다.");
            }

            if (!_floatingVm.PendingFiles.Any(static file => file.MediaType == "image/png"))
            {
                _failed.Add("이미지 붙여넣기가 첨부가 되지 않았습니다.");
            }

            foreach (var pending in _floatingVm.PendingFiles.ToList())
            {
                _floatingVm.RemovePending(pending);
            }

            _floatingVm.Body = "";
            _floatingVm.SetPanel(CapsulePanelKind.None);

            _floatingVm.Body = "세 번째 짧은 기록";
            await _floatingVm.SaveNoteAsync();
            _floatingVm.Body = "네 번째 짧은 기록";
            await _floatingVm.SaveNoteAsync();
            if (_floatingVm.RecentRows.Count != 3)
            {
                _failed.Add("최근 목록이 3개를 넘거나 비었습니다: " + _floatingVm.RecentRows.Count);
            }

            if (_floatingVm.RecentRows.All(static row => row.Title != "네 번째 짧은 기록"))
            {
                _failed.Add("방금 저장한 네 번째 기록이 최근 목록 앞에 없습니다.");
            }

            var storedNotes = (await _session.Database.Entries.ListForLocalDateAsync(todayDate))
                .Count(static entry => entry.DeletedAtUtc is null
                    && (entry.Kind == EntryKind.Note || entry.Kind == EntryKind.TaskCompleted));
            if (storedNotes <= 3)
            {
                _failed.Add("최근 3개 제한이 원본까지 줄인 것으로 보입니다: " + storedNotes);
            }

            await WaitForLayoutAsync();
            TryScreen(_floating, "04-recent-3.png");
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "04-recent-3-rtt.png"));
            var pinTarget = _floatingVm.RecentRows.FirstOrDefault(static row => row.Title == "세 번째 짧은 기록")
                ?? _floatingVm.RecentRows.Last();
            _floatingVm.OpenRecentEntry(pinTarget);
            _floatingVm.PinPeekCommand.Execute(null);
            var pinnedTitle = _floatingVm.PinnedTitle;
            _floatingVm.ClosePeekCommand.Execute(null);
            _floatingVm.Body = "고정은 그대로";
            await _floatingVm.SaveNoteAsync();
            if (_floatingVm.PinnedTitle != pinnedTitle)
            {
                _failed.Add("새 기록이 고정 대상을 바꿨습니다.");
            }

            await WaitForLayoutAsync();
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "04-board-pinned.png"));
            _floatingVm.CollapseBoardCommand.Execute(null);
            if (!_session.BoardCollapsed)
            {
                _failed.Add("접기 상태가 저장되지 않았습니다.");
            }

            _floatingVm.WorkItemTitle = "할 일 하나";
            await _floatingVm.AddWorkItemAsync();
            _floatingVm.WorkItemTitle = "할 일 둘";
            await _floatingVm.AddWorkItemAsync();
            _floatingVm.WorkItemTitle = "할 일 셋";
            await _floatingVm.AddWorkItemAsync();
            _floatingVm.TogglePanel(CapsulePanelKind.Todo);
            await WaitForLayoutAsync();
            TryScreen(_floating, "05-todo-3.png");
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "05-todo-3-rtt.png"));
            _floatingVm.SetPanel(CapsulePanelKind.None);
            while (_floatingVm.OpenWorkItems.Count > 0)
            {
                await _floatingVm.CompleteAsync(_floatingVm.OpenWorkItems[0]);
            }

            var tooLong = new string('가', AppLimits.MaxNoteBodyLength + 1);
            _floatingVm.Body = tooLong;
            await _floatingVm.SaveNoteAsync();
            if (_floatingVm.SaveLabel != "다시 기록" || !_floatingVm.HasError)
            {
                _failed.Add("저장 실패 후 다시 기록 상태를 만들지 못했습니다.");
            }

            if (_floatingVm.Body != tooLong)
            {
                _failed.Add("저장 실패 후 본문이 유지되지 않았습니다.");
            }

            await WaitForLayoutAsync();
            TryScreen(_floating, "07-save-error.png");
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "07-save-error-rtt.png"));
            _floatingVm.Body = "";
            _floatingVm.SetPanel(CapsulePanelKind.None);

            _floatingVm.Body = "";
            await WaitForLayoutAsync();

            _floatingVm.WorkItemTitle = "전원 시퀀스 재확인";
            await _floatingVm.AddWorkItemAsync();
            var open = _floatingVm.OpenWorkItems.First();
            await _floatingVm.CompleteAsync(open);
            if (!_floatingVm.ShowUndoComplete)
            {
                _failed.Add("완료 후 실행 취소가 보이지 않습니다.");
            }

            await _floatingVm.UndoCompleteAsync();
            if (_floatingVm.OpenWorkItems.Count != 1)
            {
                _failed.Add("실행 취소 후 할 일이 목록에 돌아오지 않았습니다.");
            }

            var reopenedRow = _floatingVm.OpenWorkItems.First();
            await _floatingVm.CompleteAsync(reopenedRow);
            await WaitForLayoutAsync();

            var selected = _mainVm.Rows.FirstOrDefault(static row => row.KindLabel == "메모" && row.Title.Contains("보드 전원", StringComparison.Ordinal));
            if (selected is not null)
            {
                _mainVm.Select(selected);
            }

            await WaitForLayoutAsync();
            WindowCapture.Save(_main, Path.Combine(_outputDir, "03-main-timeline.png"));
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "04-floating-after-complete.png"));
            TryScreen(_main, "03-main-timeline-screen.png");
            TryScreen(_floating, "04-floating-screen.png");

            _mainVm.CloseDetail();
            await WaitForLayoutAsync();
            WindowCapture.Save(_main, Path.Combine(_outputDir, "07-main-no-detail.png"));

            _session.SetViewMode(TimelineViewMode.ByWork);
            await WaitForLayoutAsync();
            if (_mainVm.Rows.Any(static row => row.IsGroup && row.Title == "업무 미연결"))
            {
                _failed.Add("업무별 보기에 이전 '업무 미연결' 제목이 남아 있습니다.");
            }

            WindowCapture.Save(_main, Path.Combine(_outputDir, "08-main-bywork.png"));

            _session.SetViewMode(TimelineViewMode.Panorama);
            await WaitForLayoutAsync();
            if (_mainVm.Rows.Count != 0)
            {
                _failed.Add("파노라마 보기가 구현되지 않았는데 행을 채워 완료처럼 보이게 했습니다.");
            }

            WindowCapture.Save(_main, Path.Combine(_outputDir, "09-main-panorama.png"));
            await VerifyAbFlowAsync();
            _session.SetViewMode(TimelineViewMode.Chronological);
            await WaitForLayoutAsync();
            WindowCapture.Save(_main, Path.Combine(_outputDir, "10-main-chronological.png"));
            TryScreen(_main, "10-main-chronological-screen.png");

            _main.Width = 720;
            await WaitForLayoutAsync();
            if (selected is not null)
            {
                _mainVm.Select(_mainVm.Rows.FirstOrDefault(row => row.Id == selected.Id));
            }

            await WaitForLayoutAsync();
            if (_mainVm.HasDetail && VisualTreeScan.Find<EntryDetailPane>(_main).Count(pane => pane.IsVisible) != 1)
            {
                _failed.Add("좁은 창에서 선택한 기록 상세가 보이지 않습니다.");
            }
            WindowCapture.Save(_main, Path.Combine(_outputDir, "11-main-narrow.png"));
            _main.Width = 1280;
        }
        else
        {
            var selected = _mainVm.Rows.FirstOrDefault(static row => row.KindLabel == "메모");
            if (selected is not null)
            {
                _mainVm.Select(selected);
            }

            await WaitForLayoutAsync();
            WindowCapture.Save(_main, Path.Combine(_outputDir, "05-main-after-restart.png"));
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "06-floating-after-restart.png"));
            TryScreen(_main, "05-main-restart-screen.png");
            TryScreen(_floating, "06-floating-restart-screen.png");
        }

        var today = await _session.Database.Entries.ListForLocalDateAsync(
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _session.Database.DisplayTimeZone.TimeZone).DateTime));
        var note = today.FirstOrDefault(static item => item.Kind == EntryKind.Note && item.Body == "보드 전원 시퀀스를 확인했다");
        var imageOnlyNote = today.FirstOrDefault(static item => item.Kind == EntryKind.Note && string.IsNullOrWhiteSpace(item.Body));
        var completed = today.FirstOrDefault(static item => item.Kind == EntryKind.TaskCompleted);
        var reopened = today.FirstOrDefault(static item => item.Kind == EntryKind.TaskReopened);
        var attachments = note is null ? [] : _session.Database.Entries.ListAttachments(note.Id);
        if (note is null)
        {
            _failed.Add("한글 메모가 타임라인에 없습니다.");
        }

        if (attachments.Count != 2)
        {
            _failed.Add("첨부 파일이 저장되지 않았습니다.");
        }
        else if (attachments.Any(item => !File.Exists(_session.Database.Attachments.ResolveFullPath(item))))
        {
            _failed.Add("첨부 사본 파일이 없습니다.");
        }

        if (imageOnlyNote is null)
        {
            _failed.Add("이미지 단독 기록이 없습니다.");
        }

        if (completed is null)
        {
            _failed.Add("할 일 완료 이력이 타임라인에 없습니다.");
        }

        if (reopened is null)
        {
            _failed.Add("실행 취소(다시 열림) 이력이 타임라인에 없습니다.");
        }

        await CaptureV3Async();

        if (_floatingVm.OpenWorkItems.Count != 0)
        {
            _failed.Add("완료한 할 일이 활성 목록에 남아 있습니다.");
        }

        await VerifySearchNavigationAsync();
        await VerifyPostEditRefreshAsync();
        var resultPath = Path.Combine(_outputDir, "smoke-result.txt");
        if (_failed.Count > 0)
        {
            await File.WriteAllTextAsync(resultPath, "FAIL\n" + string.Join("\n", _failed) + "\n" + string.Join("\n", _notes));
            return 2;
        }

        await File.WriteAllTextAsync(resultPath, $"""
            PASS
            noteId={note!.Id}
            completedId={completed!.Id}
            attachment={attachments[0].OriginalName}
            entries={today.Count}
            smokeScope=capsule-idle,korean-note,board-after-save,log+image,image-only-recent,clipboard-excel-original,clipboard-png,too-long-fail-retry,recent-3-keep-originals,pin-unchanged,collapse-persist,complete,undo-reopen,complete-again,chrono/bywork/panorama,ab-unlabeled-flow,detail-close,narrow-width,rtt-capture,screen-capture-attempt,capsule-520x52
            notes={string.Join(" | ", _notes)}
            """);
        return 0;
    }

    private async Task VerifyPostEditRefreshAsync()
    {
        _mainVm.OpenFlowCommand.Execute(null);
        _mainVm.SearchText = "";
        _mainVm.SearchCommand.Execute(null);
        var entry = await _session.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = Guid.NewGuid().ToString("D"), Body = "갱신 검수: 수정 전 기록"
        });
        await WaitAssistIdleAsync();
        _session.NotifyDataChanged();
        await WaitForLayoutAsync();
        var list = (ListBox)_main.FindName("TimelineList");
        list.SelectedItem = _mainVm.Rows.Single(row => row.Id == entry.Id);
        _floatingVm.OpenRecentEntry(_floatingVm.RecentRows.Single(row => row.Id == entry.Id));
        _session.NotifyDataChanged();
        await WaitForLayoutAsync();
        if (_session.SelectedEntryId != entry.Id || !_mainVm.HasDetail
            || list.SelectedItem is not TimelineRow selected || selected.Id != entry.Id)
            _failed.Add("목록 갱신이 선택한 원문을 닫거나 선택 표시를 지웠습니다.");

        const string edited = "갱신 검수: 수정한 원문이 바로 보여야 합니다";
        await _session.Database.Entries.UpdateNoteAsync(new UpdateNoteRequest(entry.Id, edited, null));
        _session.NotifyDataChanged();
        await WaitForLayoutAsync();
        if (!_floatingVm.ShowPeek || !_floatingVm.ShowRecentPanel || _floatingVm.PeekBody != edited
            || !VisualTreeScan.Find<TextBlock>(_floating).Any(block => block.IsVisible && block.Text == edited))
            _failed.Add("작은 상세에 수정 전 원문이 남아 있습니다.");
        if (_session.SelectedEntryId != entry.Id || _mainVm.DetailBody != edited)
            _failed.Add("메인 상세가 수정한 원문을 유지하지 못했습니다.");
        WindowCapture.Save(_floating, Path.Combine(_outputDir, "17-edited-peek.png"));
        TryScreen(_main, "18-selected-refresh-screen.png");

        await _session.Database.Entries.SoftDeleteNoteAsync(entry.Id);
        _session.NotifyDataChanged();
        await WaitForLayoutAsync();
        if (_floatingVm.ShowPeek || _session.SelectedEntryId == entry.Id || _mainVm.HasDetail)
            _failed.Add("삭제한 기록의 상세가 닫히지 않았습니다.");
        _mainVm.CloseDetail();
        var completedWork = (await _session.Database.WorkItems.ListByStatusAsync(WorkItemStatus.Completed)).FirstOrDefault();
        if (completedWork is not null)
        {
            _mainVm.OpenWork(completedWork.Id);
            _session.NotifyDataChanged();
            await WaitForLayoutAsync();
            if (!_mainVm.ShowWorkContext || _session.SelectedWorkItemId != completedWork.Id)
                _failed.Add("업무 상세가 데이터 갱신으로 닫혔습니다.");
            _mainVm.CloseDetail();
        }
        _notes.Add("checked=selection-survives-refresh;edited-peek;deleted-detail-closed;work-context-refresh");
    }

    private async Task VerifySearchNavigationAsync()
    {
        await WaitAssistIdleAsync();
        foreach (var page in new[] { "panorama", "todos", "settings" })
        {
            if (page == "panorama") _session.SetViewMode(TimelineViewMode.Panorama);
            else if (page == "todos") _mainVm.OpenTodosCommand.Execute(null);
            else _mainVm.OpenSettingsCommand.Execute(null);
            _mainVm.SearchText = "보드 전원";
            _mainVm.SearchCommand.Execute(null);
            await WaitForLayoutAsync();
            if (!_mainVm.IsFlow || !_mainVm.IsChronological || !_mainVm.ShowTimeline
                || !_mainVm.Rows.Any(row => row.FullBody.Contains("보드 전원", StringComparison.Ordinal)))
            {
                _failed.Add("검색 결과가 표시되지 않았습니다: " + page);
            }
        }

        var searchIds = _mainVm.Rows.Select(row => row.Id).ToArray();
        var selectedSearch = _mainVm.Rows.FirstOrDefault();
        if (selectedSearch is not null) _mainVm.Select(selectedSearch);
        _mainVm.SearchText = "아직 실행하지 않은 새 검색어";
        _session.NotifyDataChanged();
        await WaitForLayoutAsync();
        if (!searchIds.SequenceEqual(_mainVm.Rows.Select(row => row.Id))
            || !_mainVm.SummaryText.StartsWith("전체 기록에서 검색", StringComparison.Ordinal))
            _failed.Add("백그라운드 갱신이 적용된 검색 결과를 지웠습니다.");
        if (selectedSearch is not null && !_mainVm.HasDetail)
            _failed.Add("검색 결과 선택이 백그라운드 갱신으로 사라졌습니다.");
        _mainVm.SearchText = "보드 전원";
        WindowCapture.Save(_main, Path.Combine(_outputDir, "15-search-results.png"));
        TryScreen(_main, "15-search-results-window.png");
        _mainVm.SearchText = "__no_result_dc_audit_0916__";
        _mainVm.SearchCommand.Execute(null);
        await WaitForLayoutAsync();
        _session.NotifyDataChanged();
        await WaitForLayoutAsync();
        if (!_mainVm.ShowEmpty || _mainVm.EmptyPrimary != "검색 결과가 없습니다")
            _failed.Add("검색 결과 없음 상태가 보이지 않습니다.");
        _mainVm.SearchText = "";
        _mainVm.SearchCommand.Execute(null);
        _mainVm.OpenSettingsCommand.Execute(null);
        await WaitForLayoutAsync();
        if (!VisualTreeScan.Find<TextBlock>(_main).Any(text => text.Text == _mainVm.AppVersionText))
            _failed.Add("설정에 앱 버전이 표시되지 않습니다.");
        WindowCapture.Save(_main, Path.Combine(_outputDir, "16-app-version.png"));
        _mainVm.OpenFlowCommand.Execute(null);
        _session.SetViewMode(TimelineViewMode.Panorama);
        await WaitForLayoutAsync();
        if (!_mainVm.PanoramaSegments.Any(row => row.Kind == PanoramaSegmentKind.Official
            && row.StatusLabel == "다시 열림"))
            _failed.Add("파노라마에서 완료 실행 취소 이력이 누락됐습니다.");
        var reopened = _mainVm.PanoramaSegments.FirstOrDefault(row => row.Kind == PanoramaSegmentKind.Official
            && row.StatusLabel == "다시 열림");
        if (reopened is not null)
            _mainVm.TogglePanoramaSegmentCommand.Execute(reopened);
        _mainVm.CloseDetail();
        await WaitForLayoutAsync();
        var reopenedButton = VisualTreeScan.Find<Button>(_main)
            .FirstOrDefault(button => ReferenceEquals(button.CommandParameter, reopened));
        reopenedButton?.BringIntoView();
        await WaitForLayoutAsync();
        WindowCapture.Save(_main, Path.Combine(_outputDir, "17-panorama-lifecycle.png"));
        TryScreen(_main, "17-panorama-lifecycle-window.png");
        _notes.Add("verified=search-from-panorama,todos,settings;search-survives-refresh;empty-search-survives-refresh;assembly-version;panorama-reopen");
    }

    private async Task CaptureV3Async()
    {
        var created = await _session.Database.WorkItems.CreateAsync(new CreateWorkItemRequest
        {
            RequestId = Guid.NewGuid().ToString("D"),
            Title = "취소 후 재시작 오류 확인",
            IssueKey = "DEV-1234"
        });
        _floatingVm.ContinueRecording(created.WorkItem.Id, relinkExistingDraft: true);
        await WaitForLayoutAsync();
        WindowCapture.Save(_floating, Path.Combine(_outputDir, "v3-capsule-linked.png"));
        TryScreen(_floating, "v3-capsule-linked-screen.png");

        var linkedNote = Path.Combine(_session.Database.Paths.StagingDirectory, "v3-linked-note.txt");
        await File.WriteAllTextAsync(linkedNote, "UART ready");
        _floatingVm.Body = "조건 B에서만 재현됨. UART 마지막 출력 확인 필요.";
        _floatingVm.AddPending(new PendingAttachment
        {
            OriginalName = "sample-uart.log",
            SourcePath = linkedNote,
            MediaType = "text/plain"
        });
        await _floatingVm.SaveNoteAsync();
        await WaitForLayoutAsync();
        WindowCapture.Save(_floating, Path.Combine(_outputDir, "v3-capsule-saved.png"));

        var afterNote = await _session.Database.WorkItems.GetAsync(created.WorkItem.Id);
        await _session.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = Guid.NewGuid().ToString("D"),
            ExpectedVersion = afterNote!.Version,
            Text = "펌프 OFF와 상태 초기화 순서를 비교한다.",
            Reason = NextActionChangeReason.Set
        });
        _session.NotifyDataChanged();
        _mainVm.OpenWork(created.WorkItem.Id);
        await WaitForLayoutAsync();
        WindowCapture.Save(_main, Path.Combine(_outputDir, "v3-work-context.png"));
        TryScreen(_main, "v3-work-context-screen.png");

        _mainVm.EditNextActionCommand.Execute(null);
        await WaitForLayoutAsync();
        WindowCapture.Save(_main, Path.Combine(_outputDir, "v3-next-action-edit.png"));
        _mainVm.CancelNextActionCommand.Execute(null);

        var reportVm = new DayReportViewModel(_session, _session.SelectedDate, created.WorkItem.Id);
        var report = new DayReportWindow(reportVm);
        report.Show();
        await WaitForLayoutAsync();
        WindowCapture.Save(report, Path.Combine(_outputDir, "v3-day-report.png"));

        foreach (var row in reportVm.Rows.ToList())
        {
            if (row.Candidate.Kind is ReportCandidateKind.Note or ReportCandidateKind.NextAction)
            {
                reportVm.ToggleCommand.Execute(row);
            }
        }

        reportVm.RefreshPreviewCommand.Execute(null);
        await WaitForLayoutAsync();
        WindowCapture.Save(report, Path.Combine(_outputDir, "v3-day-report-preview.png"));
        report.Close();

        var current = await _session.Database.WorkItems.GetAsync(created.WorkItem.Id);
        await _session.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = Guid.NewGuid().ToString("D"),
            ExpectedVersion = current!.Version
        });
        _session.NotifyDataChanged();
        _mainVm.CloseDetail();
    }

    private void AssertCapsuleChrome()
    {
        if (_floating.WindowStyle != WindowStyle.None)
        {
            _failed.Add("플로팅 WindowStyle이 None이 아닙니다: " + _floating.WindowStyle);
        }

        if (_floating.ResizeMode != ResizeMode.NoResize)
        {
            _failed.Add("플로팅에 크기 조절이 남아 있습니다.");
        }

        if (_floating.ActualHeight > 80 && !_floatingVm.ShowAuxiliaryPanel)
        {
            _failed.Add($"패널 없는 플로팅 높이가 {_floating.ActualHeight:0.#} DIP 입니다. 68 DIP 전후여야 합니다.");
        }

        var capsule = _floating.CapsuleSurface;
        capsule.UpdateLayout();
        if (Math.Abs(capsule.ActualWidth - 520) > 2 || Math.Abs(capsule.ActualHeight - 52) > 2)
        {
            _failed.Add($"캡슐 표면 {capsule.ActualWidth:0.#}×{capsule.ActualHeight:0.#} DIP (기대 520×52)");
        }
    }

    private async Task CaptureIdleEvidenceAsync()
    {
        _floatingVm.SetPanel(CapsulePanelKind.None);
        await WaitForLayoutAsync();
        WindowCapture.TrySaveOnBackdrop(_floating, Path.Combine(_outputDir, "01-capsule-idle-light.png"), System.Drawing.Color.White, out var lightError);
        if (lightError is not null)
        {
            _notes.Add("SCREEN NOT RUN 01-capsule-idle-light: " + lightError);
        }

        WindowCapture.TrySaveOnBackdrop(_floating, Path.Combine(_outputDir, "02-capsule-idle-dark.png"), System.Drawing.Color.FromArgb(45, 45, 48), out var darkError);
        if (darkError is not null)
        {
            _notes.Add("SCREEN NOT RUN 02-capsule-idle-dark: " + darkError);
        }

        _floatingVm.Body = "지금 적는 한 줄";
        await WaitForLayoutAsync();
        TryScreen(_floating, "03-capsule-typing.png");
        WindowCapture.Save(_floating, Path.Combine(_outputDir, "03-capsule-typing-rtt.png"));
        if (_floating.ActualHeight > 80)
        {
            _failed.Add("한 줄 입력 후 창 높이가 커졌습니다: " + _floating.ActualHeight.ToString("0.#"));
        }

        _floatingVm.Body = "";
        await WaitForLayoutAsync();
        TryScreen(_floating, "08-narrow-or-high-dpi.png");
    }

    private async Task MeasureWarmShowAsync()
    {
        var left = _floating.Left;
        var top = _floating.Top;
        var samples = new List<long>();
        for (var i = 0; i < 12; i++)
        {
            _floating.Hide();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _floating.Show();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            watch.Stop();
            samples.Add(watch.ElapsedMilliseconds);
        }

        _floating.Left = left;
        _floating.Top = top;
        samples.Sort();
        var p95 = samples[(int)Math.Floor((samples.Count - 1) * 0.95)];
        await File.WriteAllTextAsync(
            Path.Combine(_outputDir, "capsule-warm-show.txt"),
            $"samplesMs={string.Join(",", samples)}{Environment.NewLine}p95Ms={p95}{Environment.NewLine}n={samples.Count}");
    }

    private async Task TraceAsync(string step)
    {
        var line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + step + Environment.NewLine;
        await File.AppendAllTextAsync(Path.Combine(_outputDir, "trace.txt"), line);
    }

    private void TryScreen(Window window, string name)
    {
        if (!WindowCapture.TrySaveFromScreen(window, Path.Combine(_outputDir, name), out var error))
        {
            _notes.Add($"SCREEN NOT RUN {name}: {error}");
        }
    }

    private static void WritePreviewPng(string path)
    {
        using var bitmap = new System.Drawing.Bitmap(64, 40);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.FromArgb(255, 29, 122, 80));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private async Task VerifyAbFlowAsync()
    {
        var zone = _session.Database.DisplayTimeZone.TimeZone;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        DateTimeOffset At(int hour, int minute)
        {
            var local = DateTime.SpecifyKind(today.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Unspecified);
            return new DateTimeOffset(local, zone.GetUtcOffset(local));
        }

        var script = new (string RequestId, int Hour, int Minute, string Body)[]
        {
            ("ab-1", 9, 0, "인버터 과전류 확인 중"),
            ("ab-2", 9, 20, "코스표 검토 요청 들어옴. 나중에 보기"),
            ("ab-3", 9, 45, "초기화 순서 확인"),
            ("ab-4", 10, 10, "순서 변경 후 재현 안 됨"),
            ("ab-5", 10, 15, "아까 받은 코스표 검토 시작"),
            ("ab-6", 11, 0, "인버터 조건 하나 더 확인"),
            ("ab-x", 11, 30, "확인")
        };
        foreach (var note in script)
        {
            await _session.Database.Entries.SaveNoteAsync(new SaveNoteRequest
            {
                RequestId = note.RequestId,
                Body = note.Body,
                OccurredAtUtc = At(note.Hour, note.Minute)
            });
        }

        await WaitAssistIdleAsync();
        _session.NotifyDataChanged();
        _session.SetViewMode(TimelineViewMode.Panorama);
        await WaitForLayoutAsync();

        var flow = _mainVm.PanoramaSegments.Where(static item => item.IsEpisode).ToList();
        var inverter = flow.Where(static item => item.Title.Contains("인버터", StringComparison.Ordinal)).ToList();
        var course = flow.Where(static item => item.Title.Contains("코스표", StringComparison.Ordinal)).ToList();
        if (inverter.Count != 2 || course.Count != 1)
        {
            _failed.Add($"A/B 구간 inverter={inverter.Count} course={course.Count} summary={_mainVm.PanoramaSummary}");
        }
        else if (inverter[0].ThreadId != inverter[1].ThreadId)
        {
            _failed.Add("처음과 마지막 인버터 구간이 다른 업무로 나뉘었습니다.");
        }
        else if (inverter[0].ThreadId == course[0].ThreadId)
        {
            _failed.Add("코스표 수행이 인버터 구간과 같은 업무로 붙었습니다.");
        }

        if (!_mainVm.PanoramaSegments.Any(static item =>
                item.IsRequest && item.Title.Contains("코스표", StringComparison.Ordinal)))
        {
            _failed.Add("코스표 요청 마커가 파노라마에 없습니다.");
        }

        var summaryHasFlow = _mainVm.PanoramaSummary.Contains("인버터", StringComparison.Ordinal)
            && _mainVm.PanoramaSummary.Contains("코스표", StringComparison.Ordinal)
            && _mainVm.PanoramaSummary.Contains('→');
        if (!summaryHasFlow)
        {
            _failed.Add("파노라마 요약이 전환 순서를 보여 주지 않습니다: " + _mainVm.PanoramaSummary);
        }

        var screen = string.Join('\n', new[] { _mainVm.PanoramaSummary }.Concat(_mainVm.PanoramaSegments.Select(static item =>
            item.Title + item.TimeLabel + item.CountLabel + item.StatusLabel)));
        foreach (var banned in new[] { "70분", "근무시간", "점심", "집중시간", "생산성" })
        {
            if (screen.Contains(banned, StringComparison.Ordinal))
            {
                _failed.Add("파노라마에 금지 문구가 있습니다: " + banned);
            }
        }

        var dayEntries = await _session.Database.Entries.ListForLocalDateAsync(today);
        if (dayEntries.Any(static item => item.Kind == EntryKind.TaskCompleted && item.Body.Contains("재현 안 됨", StringComparison.Ordinal)))
        {
            _failed.Add("재현 안 됨을 공식 완료로 바꿨습니다.");
        }

        if (inverter.Count > 0)
        {
            inverter[0].IsExpanded = true;
            var original = inverter[0].Notes.FirstOrDefault();
            if (original is not null)
            {
                _mainVm.Select(original);
            }
        }

        await WaitForLayoutAsync();
        WindowCapture.Save(_main, Path.Combine(_outputDir, "12-panorama-ab.png"));
        WindowCapture.Save(_main, Path.Combine(_outputDir, "13-panorama-expanded.png"));
        TryScreen(_main, "12-panorama-ab-screen.png");

        var toClear = dayEntries.FirstOrDefault(static item => item.Body == "초기화 순서 확인");
        if (toClear is null)
        {
            _failed.Add("연결 수정에 쓸 초기화 순서 기록이 없습니다.");
        }
        else
        {
            _session.SelectEntry(toClear.Id);
            _mainVm.ClearAssistCommand.Execute(null);
            await WaitAssistIdleAsync();
            _session.NotifyDataChanged();
            await WaitForLayoutAsync();
            var kept = _session.Database.Assist.GetAssignment(toClear.Id);
            if (kept is not { Resolution: AssignmentResolution.ManualClear, UserLocked: true })
            {
                _failed.Add("잘못된 연결을 해제한 뒤 보존되지 않았습니다.");
            }
        }

        WindowCapture.Save(_main, Path.Combine(_outputDir, "14-panorama-unlinked.png"));
        _mainVm.CloseDetail();
    }

    private async Task WaitAssistIdleAsync()
    {
        var zone = _session.Database.DisplayTimeZone.TimeZone;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        for (var i = 0; i < 80; i++)
        {
            var entries = await _session.Database.Entries.ListForLocalDateAsync(today);
            var busy = entries.Any(item =>
            {
                var job = _session.Database.Assist.LatestJob(item.Id);
                return job is { Status: AnalysisJobStatus.Pending or AnalysisJobStatus.Running or AnalysisJobStatus.RetryWait };
            });
            if (!busy)
            {
                return;
            }

            await Task.Delay(100);
        }

        _failed.Add("분석 대기열이 비지 않았습니다.");
    }

    private static async Task WaitForLayoutAsync()
    {
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await Task.Delay(200);
    }
}

internal static class PreviewSmoke
{
    public static string Run(ComponentPreviewWindow preview)
    {
        var composers = VisualTreeScan.Find<QuickCaptureComposer>(preview);
        if (composers.Count != 4)
        {
            return $"QuickCaptureComposer 개수 {composers.Count} (기대 4)";
        }

        foreach (var composer in composers)
        {
            if (Math.Abs(composer.ActualWidth - 360) > 2)
            {
                return $"Composer 폭 {composer.ActualWidth:0.#} DIP (기대 360)";
            }
        }

        var emptySave = VisualTreeScan.Find<Button>(composers[0])
            .FirstOrDefault(static button => button.Content is string text && text == "기록");
        if (emptySave is null)
        {
            return "빈 입력 기록 버튼을 찾지 못했습니다.";
        }

        if (emptySave.IsEnabled)
        {
            return "빈 입력에서 기록 버튼이 활성화되어 있습니다.";
        }

        var retry = VisualTreeScan.Find<Button>(composers[3])
            .FirstOrDefault(static button => button.Content is string text && text == "다시 기록");
        if (retry is null)
        {
            return "실패 표본의 다시 기록 버튼을 찾지 못했습니다.";
        }

        if (VisualTreeScan.Find<Button>(preview).Any(static button => button.Content is string text && text.Contains("✓ 완료", StringComparison.Ordinal)))
        {
            return "WorkItemRow에 파란 완료 버튼이 남아 있습니다.";
        }

        var attachButtons = VisualTreeScan.Find<Button>(composers[0])
            .Where(static button => button.Content is string text && text == "첨부")
            .ToList();
        if (attachButtons.Count == 0)
        {
            return "첨부 버튼을 찾지 못했습니다.";
        }

        if (VisualTreeScan.Find<Button>(composers[0]).Any(static button => button.Content is string text && text == "파일"))
        {
            return "파일 버튼 문구가 남아 있습니다.";
        }

        return "";
    }
}

public static class WindowCapture
{
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr handle, IntPtr hdcBlt, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static void Save(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
        var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(height * dpi.DpiScaleY)),
            96 * dpi.DpiScaleX,
            96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32);
        bitmap.Render(element);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    public static void Save(Window window, string path) => Save((FrameworkElement)window, path);

    public static bool TrySaveFromScreen(Window window, string path, out string? error)
    {
        try
        {
            window.UpdateLayout();
            var helper = new WindowInteropHelper(window);
            helper.EnsureHandle();
            if (helper.Handle == IntPtr.Zero)
            {
                error = "창 핸들이 없습니다.";
                return false;
            }

            if (!GetWindowRect(helper.Handle, out var rect))
            {
                error = "GetWindowRect 실패";
                return false;
            }

            var width = Math.Max(1, rect.Right - rect.Left);
            var height = Math.Max(1, rect.Bottom - rect.Top);
            using var bitmap = new System.Drawing.Bitmap(width, height);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            var hdc = graphics.GetHdc();
            var printed = false;
            try
            {
                printed = PrintWindow(helper.Handle, hdc, 2);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }

            if (!printed)
            {
                error = "PrintWindow 실패";
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TrySaveOnBackdrop(Window window, string path, System.Drawing.Color color, out string? error)
    {
        Window? backdrop = null;
        try
        {
            window.UpdateLayout();
            backdrop = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Width = 720,
                Height = 240,
                Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)),
                Topmost = false,
                ShowActivated = false
            };
            backdrop.Left = Math.Max(SystemParameters.WorkArea.Left, window.Left - 80);
            backdrop.Top = Math.Max(SystemParameters.WorkArea.Top, window.Top - 80);
            if (backdrop.Left + backdrop.Width > SystemParameters.WorkArea.Right)
            {
                backdrop.Left = SystemParameters.WorkArea.Right - backdrop.Width;
            }

            if (backdrop.Top + backdrop.Height > SystemParameters.WorkArea.Bottom)
            {
                backdrop.Top = SystemParameters.WorkArea.Bottom - backdrop.Height;
            }
            backdrop.Show();
            backdrop.Topmost = true;
            window.Topmost = true;
            window.Activate();
            window.UpdateLayout();
            System.Threading.Thread.Sleep(200);
            var helper = new WindowInteropHelper(backdrop);
            helper.EnsureHandle();
            if (!GetWindowRect(helper.Handle, out var rect))
            {
                error = "backdrop GetWindowRect 실패";
                return false;
            }

            var width = Math.Max(1, rect.Right - rect.Left);
            var height = Math.Max(1, rect.Bottom - rect.Top);
            using var bitmap = new System.Drawing.Bitmap(width, height);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            backdrop?.Close();
        }
    }
}
