using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using FlowNote.Core.Assist;
using FlowNote.Core.Time;
using FlowNote.Desktop.Preview;
using FlowNote.Desktop.Smoke;
using FlowNote.Desktop.ViewModels;
using FlowNote.Desktop.Views;
using FlowNote.Infrastructure;
using FlowNote.Infrastructure.Assist;
using FlowNote.Infrastructure.Demo;
using FlowNote.Infrastructure.Paths;
using Forms = System.Windows.Forms;

namespace FlowNote.Desktop;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private FlowNoteDatabase? _database;
    private Forms.NotifyIcon? _tray;
    private MainWindow? _main;
    private FloatingCapsuleWindow? _floating;
    private bool _exiting;
    private bool _smoke;
    private bool _trayHintShown;
    private AnalysisWorker? _worker;
    private CancellationTokenSource? _assistCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var rawArgs = e.Args ?? [];
        DispatcherUnhandledException += (_, args) =>
        {
            TryWriteCrash("dispatcher", args.Exception);
            args.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            TryWriteCrash("domain", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
            TryWriteCrash("task", args.Exception);
        var args = StartupArgs.Parse(rawArgs);
        _smoke = args.Smoke || args.AssistSmoke;
        if (args.UiPreview)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var preview = new ComponentPreviewWindow();
            MainWindow = preview;
            preview.Show();
            if (args.Smoke)
            {
                var output = args.SmokeOut ?? Path.Combine(AppContext.BaseDirectory, "smoke-output");
                Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        await Task.Delay(300);
                        Directory.CreateDirectory(output);
                        preview.UpdateLayout();
                        var check = PreviewSmoke.Run(preview);
                        WindowCapture.Save(preview, Path.Combine(output, "u0-component-preview-window.png"));
                        WindowCapture.Save(preview.CaptureRoot, Path.Combine(output, "u0-component-preview.png"));
                        if (!string.IsNullOrEmpty(check))
                        {
                            await File.WriteAllTextAsync(Path.Combine(output, "u0-result.txt"), "FAIL\n" + check);
                            Shutdown(1);
                            return;
                        }

                        await File.WriteAllTextAsync(
                            Path.Combine(output, "u0-result.txt"),
                            "PASS\npreview=u0-component-preview.png\nscope=composer-count-4,composer-width-360,empty-save-disabled,retry-label,no-complete-button,attach-not-file\nnot-asserted=IME,multi-dpi,focus-vs-other-apps,beginner-usability");
                        Shutdown(0);
                    }
                    catch (Exception ex)
                    {
                        Directory.CreateDirectory(output);
                        await File.WriteAllTextAsync(Path.Combine(output, "u0-result.txt"), "FAIL\n" + ex);
                        Shutdown(1);
                    }
                }, DispatcherPriority.ApplicationIdle);
            }

            return;
        }
        var mutexName = args.AssistSmoke
            ? @"Local\FlowNote.Desktop.AssistSmoke"
            : args.Smoke
            ? @"Local\FlowNote.Desktop.Smoke"
            : args.Demo ? @"Local\FlowNote.Desktop.Demo" : @"Local\FlowNote.Desktop.Live";
        if (!string.IsNullOrWhiteSpace(args.DataRoot))
        {
            var hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(args.DataRoot))))
                .ToLowerInvariant()[..12];
            mutexName += ".d" + hex;
        }

        _mutex = new Mutex(true, mutexName, out var created);
        if (!created)
        {
            if (args.Smoke || args.AssistSmoke)
            {
                Shutdown(3);
                return;
            }

            MessageBox.Show("FlowNote가 이미 실행 중입니다.", "FlowNote");
            Shutdown();
            return;
        }

        var mode = args.Demo || args.Smoke || args.AssistSmoke ? AppStorageMode.Demo : AppStorageMode.Live;
        if (args.Smoke || args.AssistSmoke)
        {
            mode = AppStorageMode.Demo;
        }

        var paths = AppStoragePaths.Create(mode, args.DataRoot);
        var timeZone = new DisplayTimeZone(TimeZoneInfo.Local);
        if (args.Demo && !args.Smoke && !args.AssistSmoke)
        {
            var seedClock = new AdjustableClock(DateTimeOffset.UtcNow);
            using var seedDb = new FlowNoteDatabase(paths, seedClock, timeZone);
            V3ScenarioSeeder.EnsureAsync(seedDb, value => seedClock.UtcNow = value, ResolveV3Fixtures())
                .GetAwaiter().GetResult();
        }

        if ((args.AssistDemo || args.AssistSmoke) && (args.Demo || args.AssistSmoke || !string.IsNullOrWhiteSpace(args.DataRoot)))
        {
            var seedClock = new AdjustableClock(DateTimeOffset.UtcNow);
            using var seedDb = new FlowNoteDatabase(paths, seedClock, timeZone);
            Task.Run(() => V4AssistDemoSeeder.EnsureAsync(seedDb, value => seedClock.UtcNow = value, ResolveAssistFixtures()))
                .GetAwaiter().GetResult();
            if (args.AssistSmoke && !string.IsNullOrWhiteSpace(args.SmokeOut))
            {
                Directory.CreateDirectory(args.SmokeOut);
                File.WriteAllText(Path.Combine(args.SmokeOut, "app-startup.txt"), "seeded");
            }
        }

        _database = new FlowNoteDatabase(paths, new SystemClock(), timeZone);
        var session = new AppSession(_database);
        if (!args.AssistSmoke)
        {
            StartAssistWorker(session);
        }
        var mainVm = new MainViewModel(session);
        var floatingVm = new FloatingViewModel(session);
        _main = new MainWindow(mainVm, session);
        _floating = new FloatingCapsuleWindow(floatingVm, session);
        floatingVm.OpenTodayRequested += () =>
        {
            _main?.Show();
            _main?.Activate();
        };
        floatingVm.OpenEntryRequested += row =>
        {
            session.SelectDate(row.LocalDate);
            session.SelectEntry(row.Id);
            var match = mainVm.Rows.FirstOrDefault(item => item.Id == row.Id);
            if (match is not null)
            {
                mainVm.Select(match);
            }

            _main?.Show();
            _main?.Activate();
        };
        floatingVm.ExitRequested += ExitApp;
        floatingVm.OpenWorkRequested += id =>
        {
            mainVm.OpenWork(id);
            _main?.Show();
            _main?.Activate();
        };
        mainVm.FocusCapsuleRequested += () =>
        {
            _floating?.Show();
            _floating?.Activate();
        };
        mainVm.ContinueRecordingRequested += id =>
        {
            var decision = floatingVm.PrepareContinue(id);
            if (decision == FlowNote.Core.Models.DraftWorkLinkDecision.Confirm)
            {
                var choice = MessageBox.Show(
                    _main,
                    "작성 중인 초안이 있습니다.\n예: 기존 초안 계속\n아니요: 이 초안을 새 업무에 연결\n취소: 그대로 두기",
                    "이어서 기록",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                if (choice == MessageBoxResult.Cancel)
                {
                    return;
                }

                floatingVm.ContinueRecording(id, relinkExistingDraft: choice == MessageBoxResult.No);
            }
            else
            {
                floatingVm.ContinueRecording(id, relinkExistingDraft: true);
            }

            _floating?.Show();
            _floating?.Activate();
        };
        mainVm.DayReportRequested += filter =>
        {
            var report = new Views.DayReportWindow(new DayReportViewModel(session, session.SelectedDate, filter))
            {
                Owner = _main
            };
            report.ShowDialog();
        };
        mainVm.LinkWorkRequested += entryId =>
        {
            var dialog = new Views.WorkPickerWindow(floatingVm.OpenWorkItems) { Owner = _main };
            if (dialog.ShowDialog() != true || dialog.SelectedWorkItemId is null || entryId is null)
            {
                return;
            }

            session.Database.Entries.RelinkNoteAsync(new FlowNote.Core.Models.RelinkNoteRequest(entryId, dialog.SelectedWorkItemId))
                .GetAwaiter().GetResult();
            session.NotifyDataChanged();
        };
        mainVm.AssistScopeAckRequested += () =>
        {
            var choice = MessageBox.Show(
                _main,
                "로컬 보조는 이 PC의 메모 원문과 첨부 파일명만 사용합니다. 외부 AI로 보내지 않으며, 모델이 없으면 규칙 모드로 둡니다.",
                "로컬 보조",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            if (choice == MessageBoxResult.OK)
            {
                session.Database.Assist.AcknowledgeScope();
                session.Database.Assist.SetMode(AssistMode.LocalAssist);
                session.NotifyDataChanged();
            }
        };
        _main.Closing += (_, closing) =>
        {
            if (_exiting)
            {
                return;
            }

            closing.Cancel = true;
            _main.Hide();
            ShowTrayBalloonOnce();
        };
        SetupTray();
        _main.Show();
        _floating.Show();
        if (args.AssistSmoke)
        {
            var output = args.SmokeOut ?? Path.Combine(AppContext.BaseDirectory, "assist-smoke-output");
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    var harness = new AssistSmokeHarness(session, mainVm, floatingVm, _main, _floating, output);
                    var code = await harness.RunAsync();
                    _exiting = true;
                    Shutdown(code);
                }
                catch (Exception ex)
                {
                    Directory.CreateDirectory(output);
                    await File.WriteAllTextAsync(
                        Path.Combine(output, "assist-smoke-result.txt"),
                        "FAIL\n" + ex.GetType().Name + ": " + ex.Message);
                    _exiting = true;
                    Shutdown(1);
                }
            }, DispatcherPriority.Normal);
        }
        else if (args.Smoke)
        {
            var output = args.SmokeOut ?? Path.Combine(AppContext.BaseDirectory, "smoke-output");
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    var harness = new SmokeHarness(session, mainVm, floatingVm, _main, _floating, output);
                    var code = await harness.RunAsync();
                    _exiting = true;
                    Shutdown(code);
                }
                catch (Exception ex)
                {
                    Directory.CreateDirectory(output);
                    await File.WriteAllTextAsync(Path.Combine(output, "smoke-result.txt"), "FAIL\n" + ex.GetType().Name + ": " + ex.Message);
                    _exiting = true;
                    Shutdown(1);
                }
            }, DispatcherPriority.ApplicationIdle);
        }
    }

    private static void TryWriteCrash(string kind, Exception? ex)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "flownote-crash.txt");
            File.AppendAllText(path, $"{DateTime.Now:O} {kind} {ex?.GetType().FullName}: {ex?.Message}{Environment.NewLine}{ex?.InnerException}{Environment.NewLine}{ex}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }

    private void SetupTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "FlowNote",
            Icon = SystemIcons.Application
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("오늘 보기", null, (_, _) =>
        {
            _main?.Show();
            _main?.Activate();
        });
        menu.Items.Add("기록 창", null, (_, _) =>
        {
            _floating?.Show();
            _floating?.Activate();
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("앱 종료", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) =>
        {
            _main?.Show();
            _main?.Activate();
        };
    }

    private void ShowTrayBalloonOnce()
    {
        if (_trayHintShown || _tray is null || _smoke)
        {
            return;
        }

        _trayHintShown = true;
        _tray.ShowBalloonTip(2500, "FlowNote", "창을 닫으면 트레이로 숨깁니다. 끝내려면 트레이에서 앱 종료를 누르세요.", Forms.ToolTipIcon.Info);
    }

    private void ExitApp()
    {
        _exiting = true;
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        StopAssistWorker();
        _database?.Dispose();
        _database = null;
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        StopAssistWorker();
        _database?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void StartAssistWorker(AppSession session)
    {
        IContextInference inference = UnavailableContextInference.Instance;
        try
        {
            var settings = session.Database.Assist.GetSettings();
            inference = new OllamaContextInference(settings.OllamaBaseUrl, settings.ModelTag);
        }
        catch (Exception)
        {
            inference = UnavailableContextInference.Instance;
        }

        _assistCts = new CancellationTokenSource();
        _worker = new AnalysisWorker(session.Database, inference, new SystemClock());
        _worker.Completed += () =>
        {
            try
            {
                Dispatcher.BeginInvoke(() => session.NotifyDataChanged());
            }
            catch (InvalidOperationException)
            {
            }
        };
        _ = _worker.RunAsync(_assistCts.Token);
    }

    private void StopAssistWorker()
    {
        try
        {
            _assistCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _worker?.Dispose();
        _worker = null;
        _assistCts?.Dispose();
        _assistCts = null;
    }

    private static string? ResolveV3Fixtures()
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && start is not null; i++)
        {
            var candidate = Path.Combine(start.FullName, "fixtures", "v3");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            start = start.Parent;
        }

        return null;
    }

    private static string? ResolveAssistFixtures()
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && start is not null; i++)
        {
            var candidate = Path.Combine(start.FullName, "fixtures", "assist-v4", "scenarios");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var pack = Path.Combine(start.FullName, "FlowNote_Local_Assist_V4", "fixtures", "scenarios");
            if (Directory.Exists(pack))
            {
                return pack;
            }

            start = start.Parent;
        }

        return null;
    }
}

internal sealed record StartupArgs(bool Demo, bool Smoke, bool UiPreview, bool AssistDemo, bool AssistSmoke, string? DataRoot, string? SmokeOut)
{
    public static StartupArgs Parse(IReadOnlyList<string> args)
    {
        var demo = false;
        var smoke = false;
        var uiPreview = false;
        var assistDemo = false;
        var assistSmoke = false;
        string? dataRoot = null;
        string? smokeOut = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], "--demo", StringComparison.OrdinalIgnoreCase))
            {
                demo = true;
            }
            else if (string.Equals(args[i], "--smoke", StringComparison.OrdinalIgnoreCase))
            {
                smoke = true;
            }
            else if (string.Equals(args[i], "--ui-preview", StringComparison.OrdinalIgnoreCase))
            {
                uiPreview = true;
            }
            else if (string.Equals(args[i], "--assist-demo", StringComparison.OrdinalIgnoreCase))
            {
                assistDemo = true;
            }
            else if (string.Equals(args[i], "--assist-smoke", StringComparison.OrdinalIgnoreCase))
            {
                assistSmoke = true;
            }
            else if (string.Equals(args[i], "--data-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                dataRoot = args[++i];
            }
            else if (string.Equals(args[i], "--smoke-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                smokeOut = args[++i];
            }
        }

        return new StartupArgs(demo, smoke, uiPreview, assistDemo, assistSmoke, dataRoot, smokeOut);
    }
}
