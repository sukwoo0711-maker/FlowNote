using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace FlowNote.Desktop.Views;

public sealed class ImagePreviewWindow : Window
{
    private readonly string _path;
    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
    private readonly ScrollViewer _viewport = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private bool _fit = true;
    private bool _closed;
    private double _zoom = 1;
    private string _sizeLabel = "";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public string SourcePath => _path;
    public bool ImageLoaded => _image.Source is not null;
    public double Zoom => _zoom;
    public string StatusText => _status.Text;

    public ImagePreviewWindow(string path)
    {
        _path = path;
        Title = "사진 보기";
        Width = Math.Min(1000, SystemParameters.WorkArea.Width * 0.85);
        Height = Math.Min(760, SystemParameters.WorkArea.Height * 0.85);
        MinWidth = 400;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
        Background = (Brush)Application.Current.FindResource("SurfaceBrush");
        Foreground = (Brush)Application.Current.FindResource("TextPrimaryBrush");
        System.Windows.Automation.AutomationProperties.SetName(this, "사진 확대 보기");
        var root = new DockPanel { Margin = new Thickness(12) };
        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        toolbar.Children.Add(ActionButton("축소", () => SetZoom(_zoom / 1.25)));
        toolbar.Children.Add(ActionButton("확대", () => SetZoom(_zoom * 1.25)));
        toolbar.Children.Add(ActionButton("100%", () => SetZoom(1)));
        toolbar.Children.Add(ActionButton("화면에 맞춤", Fit));
        toolbar.Children.Add(_status);
        toolbar.Children.Add(ActionButton("닫기", Close));
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        _viewport.Content = _image;
        root.Children.Add(_viewport);
        Content = root;
        _status.Text = "사진을 여는 중…";
        Loaded += (_, _) => LoadTask = LoadAsync();
        Closed += (_, _) => { _closed = true; _image.Source = null; };
        _viewport.SizeChanged += (_, _) => { if (_fit) Fit(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
            else if (e.Key is Key.Add or Key.OemPlus) { e.Handled = true; SetZoom(_zoom * 1.25); }
            else if (e.Key is Key.Subtract or Key.OemMinus) { e.Handled = true; SetZoom(_zoom / 1.25); }
        };
        _viewport.PreviewMouseWheel += (_, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            e.Handled = true;
            SetZoom(e.Delta > 0 ? _zoom * 1.25 : _zoom / 1.25);
        };
    }

    private Button ActionButton(string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 6, 0), Style = (Style)Application.Current.FindResource("SecondaryButton") };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private async Task LoadAsync()
    {
        try
        {
            var loaded = await Task.Run(() =>
            {
                if (!System.IO.Path.IsPathFullyQualified(_path) || new Uri(_path).IsUnc)
                    throw new IOException("local-file-required");
                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                var width = frame.PixelWidth;
                var height = frame.PixelHeight;
                stream.Position = 0;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (Math.Max(width, height) > 4096)
                {
                    if (width >= height) bitmap.DecodePixelWidth = 4096;
                    else bitmap.DecodePixelHeight = 4096;
                }
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return (bitmap, width, height);
            });
            if (_closed) return;
            _image.Source = loaded.bitmap;
            _sizeLabel = $"{loaded.width} × {loaded.height}" +
                (Math.Max(loaded.width, loaded.height) > 4096 ? " · 축소 미리보기" : "");
            Fit();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException or FormatException)
        {
            if (!_closed) _status.Text = "사진을 열 수 없습니다. 파일이 없거나 지원하지 않는 형식입니다.";
        }
    }
    public void Fit()
    {
        _fit = true;
        if (_image.Source is not BitmapSource bitmap || _viewport.ActualWidth <= 0 || _viewport.ActualHeight <= 0) return;
        ApplyZoom(Math.Min(4, Math.Min((_viewport.ActualWidth - 24) / bitmap.PixelWidth, (_viewport.ActualHeight - 24) / bitmap.PixelHeight)));
    }
    public void SetZoom(double zoom)
    {
        _fit = false;
        ApplyZoom(Math.Clamp(zoom, 0.05, 4));
    }
    private void ApplyZoom(double zoom)
    {
        if (_image.Source is not BitmapSource bitmap) return;
        _zoom = Math.Max(0.01, zoom);
        _image.Width = bitmap.PixelWidth * _zoom;
        _image.Height = bitmap.PixelHeight * _zoom;
        _status.Text = $"{_zoom:P0} · {_sizeLabel}";
    }
}
