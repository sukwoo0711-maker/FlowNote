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
using FlowNote.Desktop.Input;
using FlowNote.Desktop.Theming;
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
    private FlowNote.Infrastructure.Assist.Embedded.EmbeddedEngineManager? _engine;
    private GlobalHotKey? _hotKey;

    protected override void OnStartup(StartupEventArgs e)
    {
        Environment.SetEnvironmentVariable("DOTNET_EnableDiagnostics", "0");
        Environment.SetEnvironmentVariable("COMPlus_EnableDiagnostics", "0");
        Environment.SetEnvironmentVariable("DOTNET_DbgEnableMiniDump", "0");
        Environment.SetEnvironmentVariable("COMPlus_DbgEnableMiniDump", "0");
        Environment.SetEnvironmentVariable("DOTNET_EnableEventPipe", "0");
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        Environment.SetEnvironmentVariable("DOTNET_TELEMETRY_OPTOUT", "1");
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
        if (args.AssistEval || args.AssistProbe)
        {
            RunHeadlessAssist(args);
            return;
        }
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
        if (args.Demo && !args.Smoke && !args.AssistSmoke && !args.AssistLive)
        {
            var seedClock = new AdjustableClock(DateTimeOffset.UtcNow);
            using var seedDb = new FlowNoteDatabase(paths, seedClock, timeZone);
            V3ScenarioSeeder.EnsureAsync(seedDb, value => seedClock.UtcNow = value, ResolveV3Fixtures())
                .GetAwaiter().GetResult();
        }

        if (args.AssistLive && (args.Demo || !string.IsNullOrWhiteSpace(args.DataRoot)))
        {
            var seedClock = new AdjustableClock(DateTimeOffset.UtcNow);
            using var seedDb = new FlowNoteDatabase(paths, seedClock, timeZone);
            Task.Run(() => V4AssistDemoSeeder.SeedInputsOnlyAsync(seedDb, value => seedClock.UtcNow = value, ResolveAssistFixtures()))
                .GetAwaiter().GetResult();
            seedDb.Assist.AcknowledgeScope();
            seedDb.Assist.SetMode(AssistMode.LocalAssist);
        }
        else if ((args.AssistDemo || args.AssistSmoke) && (args.Demo || args.AssistSmoke || !string.IsNullOrWhiteSpace(args.DataRoot)))
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
            ApplyCoreProductScope(session);
            EnsureAssistWorker(session);
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
        mainVm.FocusCapsuleRequested += () => _floating?.FocusMemoFromUser();
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

            _floating?.FocusMemoFromUser();
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
                "로컬 보조는 이 PC 안의 메모 원문과 첨부 파일명만 사용합니다. 동봉된 로컬 엔진이 필요할 때 127.0.0.1에서만 실행되고 쉬는 동안 종료됩니다. 원문은 인터넷이나 외부 AI로 나가지 않습니다. 모델이 없으면 가져오거나 AI 없이 계속할 수 있습니다.",
                "로컬 보조",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            if (choice == MessageBoxResult.OK)
            {
                session.Database.Assist.AcknowledgeScope();
                session.Database.Assist.SetMode(AssistMode.LocalAssist);
                EnsureAssistWorker(session);
                session.NotifyDataChanged();
            }
        };
        mainVm.AssistEngineRequested += () => EnsureAssistWorker(session);
        mainVm.ImportModelRequested += () =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "GGUF (*.gguf)|*.gguf",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(_main) != true || session.Engine is null)
            {
                return;
            }

            var engine = session.Engine;
            var fileName = dialog.FileName;
            _ = Task.Run(async () =>
            {
                var imported = await engine.ImportModelAsync(fileName, null, CancellationToken.None);
                if (imported.Fingerprint is not null)
                {
                    session.Database.Assist.SetLockedDigest(imported.Fingerprint);
                }

                await Dispatcher.InvokeAsync(() => session.NotifyDataChanged());
            });
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
        if (!_smoke)
        {
            _hotKey = new GlobalHotKey(() => _floating?.FocusMemoFromUser());
            mainVm.SetCaptureHotkeyStatus(_hotKey.CaptureRegistered);
        }
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
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowNote",
                "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.txt");
            var detail = ex?.GetType().FullName;
            if (ex is System.Windows.Markup.XamlParseException && ex.InnerException is not null)
            {
                detail += " " + ex.InnerException.GetType().Name;
            }

            File.AppendAllText(path, $"{DateTime.Now:O} {kind} {detail}{Environment.NewLine}");
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
            Text = "FlowNote · " + GlobalHotKey.CaptureLabel,
            Icon = BrandIcon.LoadTrayIcon()
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("기록 창 (" + GlobalHotKey.CaptureLabel + ")", null, (_, _) =>
        {
            _floating?.FocusMemoFromUser();
        });
        menu.Items.Add("오늘 보기", null, (_, _) =>
        {
            _main?.Show();
            _main?.Activate();
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

        DisposeHotKey();
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

        DisposeHotKey();
        StopAssistWorker();
        _database?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void DisposeHotKey()
    {
        _hotKey?.Dispose();
        _hotKey = null;
    }

    private static void ApplyCoreProductScope(AppSession session)
    {
        if (session.Database.Settings.Get("product.coreBoard") != "v1")
        {
            session.Database.Settings.Set("product.coreBoard", "v1");
        }

        if (session.Database.Settings.Get("product.coreFlow") != "v2")
        {
            session.Database.Settings.Set("product.coreFlow", "v2");
            if (session.Database.Assist.GetSettings().Mode == AssistMode.Off)
            {
                session.Database.Assist.SetMode(AssistMode.RulesOnly);
            }
        }
    }

    private void EnsureAssistWorker(AppSession session)
    {
        var mode = session.Database.Assist.GetSettings().Mode;
        if (mode == AssistMode.Off)
        {
            StopAssistWorker();
            return;
        }

        var wantsEngine = mode == AssistMode.LocalAssist;
        if (_worker is not null && wantsEngine == (session.Engine is not null))
        {
            return;
        }

        StopAssistWorker();
        StartAssistWorker(session, wantsEngine);
    }

    private void StartAssistWorker(AppSession session, bool startEngine)
    {
        IContextInference inference;
        if (startEngine)
        {
            var verifier = new FlowNote.Infrastructure.Assist.Embedded.AssetVerifier(AppContext.BaseDirectory, session.Database.Paths);
            var engine = new FlowNote.Infrastructure.Assist.Embedded.EmbeddedEngineManager(verifier, session.Database.Paths);
            _engine = engine;
            session.Engine = engine;
            var check = verifier.Check();
            if (check.Fingerprint is not null)
            {
                session.Database.Assist.SetLockedDigest(check.Fingerprint);
            }

            inference = new FlowNote.Infrastructure.Assist.Embedded.ManagedLlamaInference(engine);
        }
        else
        {
            session.Engine = null;
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
        _engine?.Dispose();
        _engine = null;
        _assistCts?.Dispose();
        _assistCts = null;
    }

    private void RunHeadlessAssist(StartupArgs args)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var dataRoot = args.DataRoot ?? Path.Combine(Path.GetTempPath(), "FlowNote-v41-" + (args.AssistProbe ? "probe" : "eval"));
        var hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataRoot))))
            .ToLowerInvariant()[..12];
        _mutex = new Mutex(true, @"Local\FlowNote.Desktop.Headless.d" + hex, out var created);
        if (!created)
        {
            Shutdown(3);
            return;
        }

        var paths = AppStoragePaths.Create(AppStorageMode.Demo, dataRoot);
        var timeZone = new DisplayTimeZone(TimeZoneInfo.Local);
        var database = new FlowNoteDatabase(paths, new SystemClock(), timeZone);
        var verifier = new FlowNote.Infrastructure.Assist.Embedded.AssetVerifier(AppContext.BaseDirectory, paths);
        var engine = new FlowNote.Infrastructure.Assist.Embedded.EmbeddedEngineManager(verifier, paths);
        _engine = engine;
        var inference = new FlowNote.Infrastructure.Assist.Embedded.ManagedLlamaInference(engine);
        if (verifier.Check().Fingerprint is not null)
        {
            database.Assist.SetLockedDigest(verifier.Check().Fingerprint!);
        }

        var output = args.EvalOut
                     ?? Path.Combine(AppContext.BaseDirectory, args.AssistProbe ? "engine-probe.json" : "model-eval.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            int code;
            if (args.AssistProbe)
            {
                code = Task.Run(() => FlowNote.Infrastructure.Assist.Embedded.AssistEngineProbe
                    .RunAsync(engine, inference, output, CancellationToken.None)).GetAwaiter().GetResult();
            }
            else
            {
                var fixtures = ResolveEvalFixtures();
                if (fixtures is null)
                {
                    File.WriteAllText(output, """{"layer":"MODEL_REAL","status":"BLOCKED","reason":"eval fixtures missing"}""");
                    Shutdown(2);
                    return;
                }

                code = Task.Run(() => FlowNote.Infrastructure.Assist.Embedded.AssistEvalHarness
                    .RunAsync(inference, fixtures, output, args.EvalRepeats, CancellationToken.None)).GetAwaiter().GetResult();
            }

            Shutdown(code);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(new
            {
                status = "FAIL",
                error = ex.GetType().Name,
                message = ex.Message
            }));
            TryWriteCrash("headless", ex);
            Shutdown(1);
        }
        finally
        {
            inference.Dispose();
            engine.Dispose();
            _engine = null;
            database.Dispose();
        }
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

    private static string? ResolveEvalFixtures()
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && start is not null; i++)
        {
            var candidate = Path.Combine(start.FullName, "fixtures", "assist-v4", "eval");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            start = start.Parent;
        }

        return null;
    }
}

internal sealed record StartupArgs(
    bool Demo,
    bool Smoke,
    bool UiPreview,
    bool AssistDemo,
    bool AssistSmoke,
    bool AssistLive,
    bool AssistEval,
    bool AssistProbe,
    int EvalRepeats,
    string? DataRoot,
    string? SmokeOut,
    string? EvalOut)
{
    public static StartupArgs Parse(IReadOnlyList<string> args)
    {
        var demo = false;
        var smoke = false;
        var uiPreview = false;
        var assistDemo = false;
        var assistSmoke = false;
        var assistLive = false;
        var assistEval = false;
        var assistProbe = false;
        var evalRepeats = 3;
        string? dataRoot = null;
        string? smokeOut = null;
        string? evalOut = null;
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
            else if (string.Equals(args[i], "--assist-live", StringComparison.OrdinalIgnoreCase))
            {
                assistLive = true;
            }
            else if (string.Equals(args[i], "--assist-eval", StringComparison.OrdinalIgnoreCase))
            {
                assistEval = true;
            }
            else if (string.Equals(args[i], "--assist-engine-probe", StringComparison.OrdinalIgnoreCase))
            {
                assistProbe = true;
            }
            else if (string.Equals(args[i], "--eval-repeats", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                _ = int.TryParse(args[++i], out evalRepeats);
                if (evalRepeats < 1)
                {
                    evalRepeats = 1;
                }
            }
            else if (string.Equals(args[i], "--data-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                dataRoot = args[++i];
            }
            else if (string.Equals(args[i], "--smoke-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                smokeOut = args[++i];
            }
            else if (string.Equals(args[i], "--eval-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                evalOut = args[++i];
            }
        }

        return new StartupArgs(
            demo,
            smoke,
            uiPreview,
            assistDemo,
            assistSmoke,
            assistLive,
            assistEval,
            assistProbe,
            evalRepeats,
            dataRoot,
            smokeOut,
            evalOut);
    }
}
