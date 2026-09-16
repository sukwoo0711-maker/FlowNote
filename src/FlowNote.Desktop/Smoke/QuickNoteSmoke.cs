using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlowNote.Desktop.Controls;
using FlowNote.Desktop.Input;
using FlowNote.Desktop.Preview;
using FlowNote.Desktop.Views;
using FlowNote.Desktop.ViewModels;
namespace FlowNote.Desktop.Smoke;

internal sealed partial class SmokeHarness
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);

    private async Task VerifyQuickNoteAsync()
    {
        var callbacks = 0;
        using (var hotkey = new GlobalHotKey(() => { callbacks++; _floating.FocusMemoFromUser(); }))
        {
            _mainVm.SetCaptureHotkeyStatus(hotkey.CaptureRegistered, hotkey.ActiveLabel, hotkey.RegistrationError);
            if (!hotkey.CaptureRegistered) _failed.Add("퀵 단축키 등록 실패: " + hotkey.RegistrationError);
            else
            {
                foreach (var multiline in new[] { false, true })
                {
                    _floatingVm.Body = multiline ? "작성하던 첫 줄\n두 번째 줄" : "작성하던 메모";
                    if (multiline) _floatingVm.OpenLongNote();
                    _floating.Hide();
                    _main.Activate();
                    await WaitForLayoutAsync();
                    var key = hotkey.UsesFallback ? (byte)0x4E : (byte)0x20;
                    foreach (var vk in new byte[] { 0x11, 0x12, key }) keybd_event(vk, 0, 0, 0);
                    foreach (var vk in new byte[] { key, 0x12, 0x11 }) keybd_event(vk, 0, 2, 0);
                    await Task.Delay(350);
                    var target = VisualTreeScan.Find<TextBox>(_floating).FirstOrDefault(box => box.IsVisible && box.IsKeyboardFocused);
                    if (target is null || target.Text != _floatingVm.Body)
                        _failed.Add("퀵 포커스 실패 multiline=" + multiline);
                    WindowCapture.Save(_floating, Path.Combine(_outputDir, multiline ? "20-hotkey-multiline.png" : "19-hotkey-single.png"));
                }
                if (callbacks != 2) _failed.Add("실제 WM_HOTKEY 호출 수=" + callbacks);
            }
            await File.WriteAllTextAsync(Path.Combine(_outputDir, "hotkey-result.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                registered = hotkey.CaptureRegistered, label = hotkey.ActiveLabel, callbacks,
                primaryError = hotkey.PrimaryError, method = "keybd_event -> Windows WM_HOTKEY -> visible WPF editor",
                ime_human = "NOT RUN"
            }));
        }
        _floatingVm.Body = "";
        _floatingVm.SetPanel(FlowNote.Core.Rules.CapsulePanelKind.None);
        _session.SetViewMode(TimelineViewMode.Chronological);
        _session.NotifyDataChanged();
        _mainVm.CloseDetail();
        _main.Show();
        _main.Activate();
        await WaitForLayoutAsync();
        await VerifyImageAndStickyAsync();
        _notes.Add("checked=hotkey-single-multiline;sticky-body;image-open-zoom-close-missing");
    }
    private async Task VerifyImageAndStickyAsync()
    {
        var row = _mainVm.Rows.First(item => !item.IsEvent && item.ShowHeroImage);
        var list = (ListBox)_main.FindName("TimelineList");
        list.ScrollIntoView(row);
        await WaitForLayoutAsync();
        var notes = VisualTreeScan.Find<StickyNoteContent>(_main).Where(item => item.IsVisible && ReferenceEquals(item.DataContext, row)).ToList();
        if (notes.Count == 0 || VisualTreeScan.Find<TextBlock>(notes[0]).Count(t => t.Text == row.FullBody) != 1)
            _failed.Add("포스트잇 본문이 없거나 제목으로 중복되었습니다.");
        WindowCapture.Save(_main, Path.Combine(_outputDir, "21-sticky-day.png"));
        var image = VisualTreeScan.Find<Image>(_main).FirstOrDefault(item => item.IsVisible && ImagePreview.GetPath(item) == row.HeroImagePath);
        if (image is null) _failed.Add("이미지 확대 진입점을 찾지 못했습니다.");
        else
        {
            image.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
            await WaitForLayoutAsync();
            var viewer = Application.Current.Windows.OfType<ImagePreviewWindow>().SingleOrDefault();
            if (viewer is null) _failed.Add("이미지 클릭으로 확대 창이 열리지 않았습니다.");
            else
            {
                await viewer.LoadTask;
                if (!viewer.ImageLoaded) _failed.Add("확대 창 이미지 로딩 실패: " + viewer.StatusText);
                viewer.SetZoom(1.5);
                if (Math.Abs(viewer.Zoom - 1.5) > 0.01) _failed.Add("확대 배율 반영 실패");
                viewer.Fit();
                await WaitForLayoutAsync();
                WindowCapture.Save(viewer, Path.Combine(_outputDir, "23-photo-zoom.png"));
                viewer.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(viewer), 0, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                if (viewer.IsVisible) _failed.Add("Escape로 확대 창이 닫히지 않았습니다.");
            }
        }
        var missing = new ImagePreviewWindow(Path.Combine(_outputDir, "does-not-exist.png")) { Owner = _main };
        missing.Show();
        await WaitForLayoutAsync();
        await missing.LoadTask;
        if (missing.ImageLoaded || !missing.StatusText.Contains("열 수 없습니다", StringComparison.Ordinal))
            _failed.Add("없는 이미지 오류 처리가 없습니다.");
        missing.Close();
        _session.SetViewMode(TimelineViewMode.Panorama);
        _mainVm.CloseDetail();
        await WaitForLayoutAsync();
        foreach (var scroll in VisualTreeScan.Find<ScrollViewer>(_main)) scroll.ScrollToTop();
        await WaitForLayoutAsync();
        WindowCapture.Save(_main, Path.Combine(_outputDir, "22-sticky-panorama.png"));
        TryScreen(_main, "22-sticky-panorama-screen.png");
    }
}
