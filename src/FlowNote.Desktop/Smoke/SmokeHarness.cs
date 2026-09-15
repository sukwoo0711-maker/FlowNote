using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

            _floatingVm.Body = "세 번째 짧은 기록";
            await _floatingVm.SaveNoteAsync();
            _floatingVm.TogglePanel(CapsulePanelKind.Recent);
            await WaitForLayoutAsync();
            TryScreen(_floating, "04-recent-3.png");
            WindowCapture.Save(_floating, Path.Combine(_outputDir, "04-recent-3-rtt.png"));
            _floatingVm.SetPanel(CapsulePanelKind.None);

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
            smokeScope=capsule-idle,korean-note,log+image,image-only,too-long-fail-retry,complete,undo-reopen,complete-again,chrono/bywork/panorama,detail-close,narrow-width,rtt-capture,screen-capture-attempt,capsule-520x52
            notes={string.Join(" | ", _notes)}
            """);
        return 0;
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
