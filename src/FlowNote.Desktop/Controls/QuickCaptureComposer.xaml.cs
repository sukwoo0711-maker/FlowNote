using System.Windows;
using System.Windows.Controls;
using FlowNote.Core.Models;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Controls;

public partial class QuickCaptureComposer : UserControl
{
    public static readonly RoutedEvent AttachRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(AttachRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(QuickCaptureComposer));

    public QuickCaptureComposer()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdatePlaceholder();
        DataContextChanged += (_, _) => Dispatcher.BeginInvoke((Action)UpdatePlaceholder);
    }

    public event RoutedEventHandler AttachRequested
    {
        add => AddHandler(AttachRequestedEvent, value);
        remove => RemoveHandler(AttachRequestedEvent, value);
    }

    public event TextChangedEventHandler? BodyTextChanged;

    private void OnBodyChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePlaceholder();
        BodyTextChanged?.Invoke(sender, e);
    }

    private void OnAttachClick(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(AttachRequestedEvent, this));

    private void OnTileRemove(object sender, RoutedEventArgs e)
    {
        if (sender is AttachmentTile { DataContext: PendingAttachment pending }
            && DataContext is IPendingAttachmentHost host)
        {
            host.RemovePending(pending);
        }
    }

    private void UpdatePlaceholder()
    {
        if (BodyBox is null || Placeholder is null)
        {
            return;
        }

        Placeholder.Visibility = string.IsNullOrEmpty(BodyBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }
}
