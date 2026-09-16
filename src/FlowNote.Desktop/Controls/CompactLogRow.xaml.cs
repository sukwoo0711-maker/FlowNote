using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Controls;

public partial class CompactLogRow : UserControl
{
    public static readonly RoutedEvent OpenRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(OpenRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(CompactLogRow));

    public CompactLogRow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind();
        Loaded += (_, _) => Bind();
    }

    public event RoutedEventHandler OpenRequested
    {
        add => AddHandler(OpenRequestedEvent, value);
        remove => RemoveHandler(OpenRequestedEvent, value);
    }

    private void OnClick(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(OpenRequestedEvent, this));

    private void Bind()
    {
        if (KindIcon is null || DataContext is not CapsuleRecentRow row)
        {
            return;
        }

        KindIcon.Data = Geometry.Parse(row.IsCompleted
            ? "M2,6 L5,9 L10,3"
            : row.IsEvent
                ? "M3,3 H9 V9 H3 Z"
                : "M2,2 H10 L12,4 V12 H2 Z");
        KindIcon.Fill = row.IsCompleted
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF75D5B0")!)
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD1E0E7")!);

        if (!string.IsNullOrWhiteSpace(row.FileName) && string.IsNullOrWhiteSpace(row.ImagePath))
        {
            FileLabel.Text = row.FileName;
            FileLabel.Visibility = Visibility.Visible;
        }
        else
        {
            FileLabel.Visibility = Visibility.Collapsed;
        }

        ImagePreview.SetPath(Thumb, row.ImagePath);
        var image = CapsuleImageLoader.TryLoad(row.ImagePath, 48);
        if (image is null)
        {
            Thumb.Visibility = Visibility.Collapsed;
            return;
        }

        Thumb.Source = image;
        Thumb.Visibility = Visibility.Visible;
    }
}
