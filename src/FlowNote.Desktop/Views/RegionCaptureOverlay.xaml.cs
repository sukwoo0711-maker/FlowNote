using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowNote.Desktop.Views;

public partial class RegionCaptureOverlay : Window
{
    private Point _origin;
    private bool _dragging;

    public RegionCaptureOverlay()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public event Action<Rect>? SelectionCompleted;

    public event Action? Canceled;

    public Rect DeviceSelection { get; private set; }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Canceled?.Invoke();
        }
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _origin = e.GetPosition(Surface);
        _dragging = true;
        CaptureMouse();
        Rubber.Visibility = Visibility.Visible;
        UpdateRubber(e.GetPosition(Surface));
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        UpdateRubber(e.GetPosition(Surface));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();
        var dip = CurrentRect(e.GetPosition(Surface));
        if (dip.Width < 2 || dip.Height < 2)
        {
            Canceled?.Invoke();
            return;
        }

        var topLeft = PointToScreen(new Point(dip.X, dip.Y));
        var bottomRight = PointToScreen(new Point(dip.X + dip.Width, dip.Y + dip.Height));
        DeviceSelection = new Rect(topLeft, bottomRight);
        SelectionCompleted?.Invoke(DeviceSelection);
    }

    private void UpdateRubber(Point current)
    {
        var rect = CurrentRect(current);
        Canvas.SetLeft(Rubber, rect.X);
        Canvas.SetTop(Rubber, rect.Y);
        Rubber.Width = Math.Max(1, rect.Width);
        Rubber.Height = Math.Max(1, rect.Height);
    }

    private Rect CurrentRect(Point current)
    {
        var x = Math.Min(_origin.X, current.X);
        var y = Math.Min(_origin.Y, current.Y);
        return new Rect(x, y, Math.Abs(current.X - _origin.X), Math.Abs(current.Y - _origin.Y));
    }
}
