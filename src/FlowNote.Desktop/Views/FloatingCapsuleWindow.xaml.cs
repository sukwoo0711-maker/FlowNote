using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Desktop.Capture;
using FlowNote.Desktop.ViewModels;
using Microsoft.Win32;

namespace FlowNote.Desktop.Views;

public partial class FloatingCapsuleWindow : Window
{
    private readonly FloatingViewModel _viewModel;
    private readonly AppSession _session;
    private readonly DispatcherTimer _draftTimer;
    private readonly DispatcherTimer _flashTimer;
    private readonly IRegionCaptureService _capture = new GdiRegionCaptureService();
    private bool _capturing;
    private bool _placing;

    public FloatingCapsuleWindow(FloatingViewModel viewModel, AppSession session)
    {
        _viewModel = viewModel;
        _session = session;
        DataContext = viewModel;
        ShowActivated = false;
        InitializeComponent();
        Width = CapsuleLayout.HostWidth;
        Height = CapsuleLayout.HostHeightWithoutPanel;
        Topmost = session.PinFloating;
        ApplyInitialPosition();
        _draftTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _draftTimer.Tick += (_, _) =>
        {
            _draftTimer.Stop();
            _viewModel.SaveDraft();
        };
        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _flashTimer.Tick += (_, _) =>
        {
            _flashTimer.Stop();
            _viewModel.ClearSuccessFlash();
        };
        session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(AppSession.PinFloating))
            {
                Topmost = session.PinFloating;
            }
        };
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.PanelChanged += () => Dispatcher.BeginInvoke(ApplyPanelPlacement, DispatcherPriority.Loaded);
        _viewModel.SuccessFlashRequested += () =>
        {
            _flashTimer.Stop();
            _flashTimer.Start();
        };
        _viewModel.HideRequested += Hide;
        _viewModel.AttachRequested += PickFiles;
        _viewModel.RegionCaptureRequested += () => _ = CaptureRegionAsync();
        _viewModel.StorageRequested += () =>
            MessageBox.Show(this, _session.Database.Paths.Root, "저장 위치");
        _viewModel.PickWorkRequested += PickWork;
        _viewModel.FocusInputRequested += () =>
        {
            Show();
            Activate();
            InputBar.MemoBox.Focus();
        };
    }

    public FrameworkElement CapsuleSurface => CapsuleChrome;

    public string ReadMetricsText()
    {
        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        GetWindowRect(helper.Handle, out var rect);
        var dpi = VisualTreeHelper.GetDpi(this);
        return string.Join(Environment.NewLine, new[]
        {
            "layoutVersion=" + CapsuleLayout.LayoutVersion,
            "windowStyle=" + WindowStyle,
            "resizeMode=" + ResizeMode,
            "allowsTransparency=" + AllowsTransparency,
            "showInTaskbar=" + ShowInTaskbar,
            "capsuleDip=" + CapsuleChrome.ActualWidth.ToString("0.#", CultureInfo.InvariantCulture) + "x" + CapsuleChrome.ActualHeight.ToString("0.#", CultureInfo.InvariantCulture),
            "windowDip=" + ActualWidth.ToString("0.#", CultureInfo.InvariantCulture) + "x" + ActualHeight.ToString("0.#", CultureInfo.InvariantCulture),
            "osBoundsPx=" + (rect.Right - rect.Left) + "x" + (rect.Bottom - rect.Top) + " at " + rect.Left + "," + rect.Top,
            "dpiScaleX=" + dpi.DpiScaleX.ToString("0.###", CultureInfo.InvariantCulture),
            "dpiScaleY=" + dpi.DpiScaleY.ToString("0.###", CultureInfo.InvariantCulture),
            "pixelsPerDip=" + dpi.PixelsPerDip.ToString("0.###", CultureInfo.InvariantCulture),
            "panelKind=" + _viewModel.PanelKind,
            "topmost=" + Topmost
        });
    }

    private void ApplyInitialPosition()
    {
        var work = SystemParameters.WorkArea;
        if (_session.TryReadFloatingPosition(out var left, out var top))
        {
            Left = left;
            Top = top;
        }
        else
        {
            Left = work.Right - CapsuleLayout.HostWidth - 24;
            Top = work.Top + 24;
        }

        ClampToWorkArea();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SystemParameters.HighContrast)
        {
            CapsuleChrome.Background = (Brush)FindResource("CapsuleOpaqueBrush");
        }

        ApplyPanelPlacement();
        ClampToWorkArea();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FloatingViewModel.Body))
        {
            _draftTimer.Stop();
            _draftTimer.Start();
        }
    }

    private void ApplyPanelPlacement()
    {
        _placing = true;
        var show = _viewModel.ShowAuxiliaryPanel;
        var work = SystemParameters.WorkArea;
        var chromeOffset = CapsuleChrome.TranslatePoint(new Point(0, 0), this).Y;
        var capsuleTop = Top + chromeOffset;
        var capsuleBottom = capsuleTop + CapsuleLayout.SurfaceHeight;
        var estimatedPanel = show ? Math.Min(CapsuleLayout.MaxEditVisibleHeight, 160) : 0;
        var openAbove = show && CapsuleLayout.ShouldOpenPanelAbove(capsuleBottom, estimatedPanel, work.Bottom);
        PanelAbove.Visibility = show && openAbove ? Visibility.Visible : Visibility.Collapsed;
        PanelBelow.Visibility = show && !openAbove ? Visibility.Visible : Visibility.Collapsed;
        UpdateLayout();
        var chromeOffsetAfter = CapsuleChrome.TranslatePoint(new Point(0, 0), this).Y;
        Top = capsuleTop - chromeOffsetAfter;
        Height = Math.Ceiling(LayoutStack.ActualHeight + 16);
        Width = CapsuleLayout.HostWidth;
        ClampToWorkArea();
        _placing = false;
    }

    private void ClampToWorkArea()
    {
        var work = SystemParameters.WorkArea;
        if (Width > work.Width - 8)
        {
            Width = Math.Max(MinWidth, work.Width - 8);
        }

        if (Left + Width > work.Right)
        {
            Left = work.Right - Width - 8;
        }

        if (Top + Height > work.Bottom)
        {
            Top = work.Bottom - Height - 8;
        }

        if (Left < work.Left)
        {
            Left = work.Left + 8;
        }

        if (Top < work.Top)
        {
            Top = work.Top + 8;
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (_placing || !IsLoaded)
        {
            return;
        }

        _session.SaveFloatingPosition(Left, Top);
    }

    private void OnCapsuleSurfaceDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.TextBox or System.Windows.Controls.Button or System.Windows.Controls.CheckBox)
        {
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void PickWork()
    {
        var dialog = new WorkPickerWindow(_viewModel.OpenWorkItems) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.Unlink)
        {
            _viewModel.ClearLinkedWork();
            return;
        }

        if (!string.IsNullOrWhiteSpace(dialog.NewWorkTitle))
        {
            _viewModel.WorkItemTitle = dialog.NewWorkTitle;
            await _viewModel.AddWorkItemAsync();
            var created = _viewModel.OpenWorkItems.LastOrDefault();
            if (created is not null)
            {
                _viewModel.ContinueRecording(created.Id, relinkExistingDraft: true);
            }

            return;
        }

        if (dialog.SelectedWorkItemId is not null)
        {
            _viewModel.ContinueRecording(dialog.SelectedWorkItemId, relinkExistingDraft: true);
        }
    }

    private void OnLogoMenu(object? sender, EventArgs e)
    {
        LogoMenu.PlacementTarget = InputBar;
        LogoMenu.IsOpen = true;
    }

    private void OnMenuCommand(object sender, RoutedEventArgs e) => LogoMenu.IsOpen = false;

    private void OnClosing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_capturing || _viewModel.IsIdle == false || LogoMenu.IsOpen)
        {
            return;
        }

        _viewModel.CloseTransientPanel();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (LogoMenu.IsOpen)
        {
            LogoMenu.IsOpen = false;
            e.Handled = true;
            return;
        }

        if (_viewModel.ShowAuxiliaryPanel)
        {
            _viewModel.SetPanel(CapsulePanelKind.None);
            e.Handled = true;
        }
    }

    private async void OnPasteImage(object? sender, EventArgs e)
    {
        await Dispatcher.Yield(DispatcherPriority.Input);
        TryPasteImage();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        _viewModel.IsDraggingFiles = e.Effects == DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => _viewModel.IsDraggingFiles = false;

    private void OnDrop(object sender, DragEventArgs e)
    {
        _viewModel.IsDraggingFiles = false;
        if (!_viewModel.IsIdle || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        AddFiles(paths);
    }

    public void PickFiles()
    {
        if (!_viewModel.IsIdle)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "첨부할 파일 선택",
            Multiselect = true,
            Filter = "모든 파일 (*.*)|*.*"
        };
        _viewModel.BeginAttach();
        try
        {
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            AddFiles(dialog.FileNames);
        }
        finally
        {
            _viewModel.EndAttach();
        }
    }

    public async Task CaptureRegionAsync()
    {
        if (_capturing || !_viewModel.IsIdle)
        {
            return;
        }

        _capturing = true;
        _viewModel.SaveDraft();
        var restoreLeft = Left;
        var restoreTop = Top;
        Hide();
        try
        {
            var result = await _capture.CaptureAsync(null);
            Show();
            Left = restoreLeft;
            Top = restoreTop;
            if (result is null)
            {
                return;
            }

            _viewModel.AddPending(new PendingAttachment
            {
                OriginalName = $"capture-{DateTime.Now:HHmmss}.png",
                SourcePath = result.StagingPath,
                MediaType = "image/png"
            });
        }
        catch (Exception ex)
        {
            Show();
            Left = restoreLeft;
            Top = restoreTop;
            _viewModel.SetError("화면을 캡처하지 못했습니다. 입력은 그대로 둡니다. " + ex.Message);
        }
        finally
        {
            _capturing = false;
        }
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        _viewModel.BeginAttach();
        try
        {
            foreach (var path in paths)
            {
                _viewModel.AddPending(new PendingAttachment
                {
                    OriginalName = Path.GetFileName(path),
                    SourcePath = path,
                    MediaType = AttachmentRules.GuessMediaType(path)
                });
            }
        }
        finally
        {
            _viewModel.EndAttach();
        }
    }

    private bool TryPasteImage()
    {
        if (!_viewModel.IsIdle || !Clipboard.ContainsImage())
        {
            return false;
        }

        var image = Clipboard.GetImage();
        if (image is null)
        {
            return false;
        }

        var staging = Path.Combine(Path.GetTempPath(), "FlowNotePaste", Guid.NewGuid().ToString("N") + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(staging)!);
        using (var stream = File.Create(staging))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }

        _viewModel.AddPending(new PendingAttachment
        {
            OriginalName = $"paste-{DateTime.Now:HHmmss}.png",
            SourcePath = staging,
            MediaType = "image/png"
        });
        return true;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
