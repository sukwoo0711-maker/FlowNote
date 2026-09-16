using System.Windows;
using System.Windows.Input;
using FlowNote.Desktop.Views;
namespace FlowNote.Desktop.Controls;

public static class ImagePreview
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
        "Path", typeof(string), typeof(ImagePreview), new PropertyMetadata(null, OnPathChanged));
    public static void SetPath(DependencyObject target, string? path) => target.SetValue(PathProperty, path);
    public static string? GetPath(DependencyObject target) => (string?)target.GetValue(PathProperty);

    private static void OnPathChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement image) return;
        image.PreviewMouseLeftButtonDown -= OnDown;
        image.PreviewMouseLeftButtonUp -= OnClick;
        image.KeyDown -= OnKey;
        if (string.IsNullOrWhiteSpace(args.NewValue as string)) return;
        image.Cursor = Cursors.Hand;
        image.Focusable = true;
        image.ToolTip = "사진 크게 보기 · Enter";
        System.Windows.Automation.AutomationProperties.SetName(image, "사진 크게 보기");
        image.PreviewMouseLeftButtonDown += OnDown;
        image.PreviewMouseLeftButtonUp += OnClick;
        image.KeyDown += OnKey;
    }
    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        ((UIElement)sender).Focus();
        e.Handled = true;
    }
    private static void OnClick(object sender, MouseButtonEventArgs e)
    {
        Open((FrameworkElement)sender);
        e.Handled = true;
    }
    private static void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space)) return;
        Open((FrameworkElement)sender);
        e.Handled = true;
    }
    internal static ImagePreviewWindow? Open(FrameworkElement image)
    {
        var path = GetPath(image);
        if (string.IsNullOrWhiteSpace(path)) return null;
        var existing = Application.Current.Windows.OfType<ImagePreviewWindow>()
            .FirstOrDefault(window => string.Equals(window.SourcePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { existing.Activate(); return existing; }
        var viewer = new ImagePreviewWindow(path);
        var owner = Window.GetWindow(image);
        if (owner is { IsVisible: true }) viewer.Owner = owner;
        viewer.Show();
        return viewer;
    }
}
