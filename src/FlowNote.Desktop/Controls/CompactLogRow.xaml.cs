using System.Windows;
using System.Windows.Controls;
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
        if (DataContext is not CapsuleRecentRow row)
        {
            return;
        }

        DoneMark.Visibility = row.IsCompleted ? Visibility.Visible : Visibility.Collapsed;

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
