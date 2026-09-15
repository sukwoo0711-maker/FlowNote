using System.Windows;
using System.Windows.Controls;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Controls;

public partial class WorkItemRow : UserControl
{
    public static readonly RoutedEvent CompleteRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(CompleteRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(WorkItemRow));

    public WorkItemRow()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler CompleteRequested
    {
        add => AddHandler(CompleteRequestedEvent, value);
        remove => RemoveHandler(CompleteRequestedEvent, value);
    }

    private void OnCheckClick(object sender, RoutedEventArgs e)
    {
        if (CompleteBox.IsChecked != true)
        {
            CompleteBox.IsChecked = false;
            return;
        }

        if (DataContext is OpenWorkItemRow { IsBusy: true })
        {
            return;
        }

        RaiseEvent(new RoutedEventArgs(CompleteRequestedEvent, this));
    }
}
