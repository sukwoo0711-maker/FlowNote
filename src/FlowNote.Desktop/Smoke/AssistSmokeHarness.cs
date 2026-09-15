using System.IO;
using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Desktop.ViewModels;
using FlowNote.Desktop.Views;

namespace FlowNote.Desktop.Smoke;

internal sealed class AssistSmokeHarness
{
    private readonly AppSession _session;
    private readonly MainViewModel _mainVm;
    private readonly FloatingViewModel _floatingVm;
    private readonly MainWindow _main;
    private readonly FloatingCapsuleWindow _floating;
    private readonly string _outputDir;
    private readonly List<string> _failed = [];

    public AssistSmokeHarness(
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
        await File.WriteAllTextAsync(Path.Combine(_outputDir, "assist-smoke-started.txt"), "started");
        _session.DismissOnboarding();
        _main.Show();
        _floating.Show();
        await WaitAsync();
        AssertCapsule();

        _session.SelectDate(new DateOnly(2026, 9, 15));
        await WaitAsync();

        var entries = await _session.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 15));
        var notes = entries.Where(static item => item.Kind == EntryKind.Note).ToList();
        if (notes.Count != 5)
        {
            _failed.Add($"2026-09-15 메모 {notes.Count}개 (기대 5)");
        }

        if (_mainVm.MinimapLanes.Count != 2)
        {
            _failed.Add($"미니맵 lane {_mainVm.MinimapLanes.Count}개 (기대 2)");
        }
        else
        {
            if (_mainVm.MinimapLanes[0].Nodes.All(static item => item.Kind != "gap"))
            {
                _failed.Add("첫 수행 lane에 관측 공백 표시가 없습니다.");
            }

            if (_mainVm.MinimapLanes[1].Nodes.All(static item => item.Kind != "request"))
            {
                _failed.Add("요청 lane에 요청 마커가 없습니다.");
            }

            if (_mainVm.MinimapLanes[0].Nodes.Any(static item => item.Kind == "request"))
            {
                _failed.Add("요청이 수행 lane을 끊는 마커로 들어갔습니다.");
            }
        }

        if (!_mainVm.MinimapLegend.Contains("기록 기반", StringComparison.Ordinal))
        {
            _failed.Add("미니맵 범례가 기록 기반이 아닙니다.");
        }

        TrySave(_main, "v4-minimap-ab.png");

        var joined = string.Join('\n',
            _mainVm.Rows.Select(static item => item.Title + item.Preview + item.AssistBadgeText + item.AssistDetailText)
                .Concat(_mainVm.MinimapLanes.Select(static item => item.Title)));
        foreach (var banned in new[] { "점심", "집중", "92%", "추천", "근무시간" })
        {
            if (joined.Contains(banned, StringComparison.Ordinal))
            {
                _failed.Add("금지 문구가 화면에 있습니다: " + banned);
            }
        }

        var requestRow = _mainVm.Rows.FirstOrDefault(static row =>
            row.AssistBadgeText.Contains("요청", StringComparison.Ordinal));
        if (requestRow is null)
        {
            _failed.Add("요청 배지 타임라인 행이 없습니다.");
        }
        else
        {
            _mainVm.Select(requestRow);
            await WaitAsync();
            TrySave(_main, "v4-timeline-request.png");
            if (requestRow.AssistBadgeText.Contains("수행", StringComparison.Ordinal) &&
                requestRow.AssistBadgeText.Contains("요청", StringComparison.Ordinal) is false)
            {
                _failed.Add("요청 기록이 수행으로만 표시됩니다.");
            }
        }

        var completionRow = _mainVm.Rows.FirstOrDefault(static row =>
            row.AssistBadgeText.Contains("완료 언급", StringComparison.Ordinal));
        if (completionRow is null)
        {
            _failed.Add("완료 언급 배지가 없습니다.");
        }

        if (requestRow is not null)
        {
            _mainVm.Select(requestRow);
            await WaitAsync();
            _mainVm.ClearAssistCommand.Execute(null);
            await WaitAsync();
            TrySave(_main, "v4-detail-correction.png");
            var assignment = _session.Database.Assist.GetAssignment(requestRow.Id);
            if (assignment is not { Resolution: AssignmentResolution.ManualClear, UserLocked: true })
            {
                _failed.Add("연결 해제 후 lock/manual_clear가 아닙니다.");
            }
        }

        _mainVm.OpenSettingsCommand.Execute(null);
        await WaitAsync();
        TrySave(_main, "v4-settings.png");
        if (_mainVm.AssistModeText != "규칙만")
        {
            _failed.Add("기본 보조 모드가 규칙만이 아닙니다: " + _mainVm.AssistModeText);
        }

        if (!_mainVm.AssistModelText.Contains("외부 AI 없음", StringComparison.Ordinal))
        {
            _failed.Add("설정에 외부 AI 없음 안내가 없습니다.");
        }

        _mainVm.OpenFlowCommand.Execute(null);
        _session.SelectDate(new DateOnly(2026, 9, 15));
        await WaitAsync();

        _floatingVm.Body = "추가한 한 줄 기록";
        await _floatingVm.SaveNoteAsync();
        await WaitAsync();
        if (_floatingVm.HasError || !_floatingVm.StatusText.Contains("기록됨", StringComparison.Ordinal))
        {
            _failed.Add("캡슐 저장이 분석을 기다리다 실패한 것으로 보입니다: " + _floatingVm.StatusText);
        }

        TrySave(_floating, "v4-capsule-saved.png");
        AssertCapsule();

        _main.Width = 720;
        await WaitAsync();
        TrySave(_main, "v4-narrow.png");
        _main.Width = 1280;

        var resultPath = Path.Combine(_outputDir, "assist-smoke-result.txt");
        if (_failed.Count > 0)
        {
            await File.WriteAllTextAsync(resultPath, "FAIL\n" + string.Join("\n", _failed));
            return 2;
        }

        await File.WriteAllTextAsync(resultPath, """
            PASS
            layer=WINDOWS_UI
            scenario=A_deferred seeded expected JSON (not MODEL_REAL)
            notes=5
            lanes=2
            capsule=520x52
            settings=rules_only
            correction=unlink+lock
            """);
        return 0;
    }

    private void TrySave(System.Windows.Window window, string name)
    {
        try
        {
            WindowCapture.Save(window, Path.Combine(_outputDir, name));
        }
        catch (Exception ex)
        {
            _failed.Add("캡처 실패 " + name + ": " + ex.Message);
        }
    }

    private void AssertCapsule()
    {
        var capsule = _floating.CapsuleSurface;
        capsule.UpdateLayout();
        if (Math.Abs(capsule.ActualWidth - 520) > 2 || Math.Abs(capsule.ActualHeight - 52) > 2)
        {
            _failed.Add($"캡슐 {capsule.ActualWidth:0.#}×{capsule.ActualHeight:0.#} DIP (기대 520×52)");
        }
    }

    private static async Task WaitAsync()
    {
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        await Task.Delay(200);
    }
}
