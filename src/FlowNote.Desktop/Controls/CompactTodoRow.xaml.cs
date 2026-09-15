using System.Windows;
using System.Windows.Controls;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Controls;

public partial class CompactTodoRow : UserControl
{
    public static readonly RoutedEvent CompleteRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(CompleteRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(CompactTodoRow));

    public static readonly RoutedEvent OpenRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(OpenRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(CompactTodoRow));

    public CompactTodoRow()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler CompleteRequested
    {
        add => AddHandler(CompleteRequestedEvent, value);
        remove => RemoveHandler(CompleteRequestedEvent, value);
    }

    public event RoutedEventHandler OpenRequested
    {
        add => AddHandler(OpenRequestedEvent, value);
        remove => RemoveHandler(OpenRequestedEvent, value);
    }

    private void OnCheck(object sender, RoutedEventArgs e)
    {
        if (DataContext is OpenWorkItemRow { IsBusy: true })
        {
            return;
        }

        RaiseEvent(new RoutedEventArgs(CompleteRequestedEvent, this));
    }

    private void OnOpen(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(OpenRequestedEvent, this));
}
