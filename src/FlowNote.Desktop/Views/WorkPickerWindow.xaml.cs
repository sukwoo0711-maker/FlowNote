using System.Windows;
using System.Windows.Input;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Views;

public partial class WorkPickerWindow : Window
{
    public WorkPickerWindow(IEnumerable<OpenWorkItemRow> items)
    {
        InitializeComponent();
        WorkList.ItemsSource = items.ToList();
    }

    public string? SelectedWorkItemId { get; private set; }

    public string? NewWorkTitle { get; private set; }

    public bool Unlink { get; private set; }

    private void OnPick(object sender, MouseButtonEventArgs e)
    {
        if (WorkList.SelectedItem is OpenWorkItemRow row)
        {
            SelectedWorkItemId = row.Id;
            DialogResult = true;
        }
    }

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        NewWorkTitle = NewTitle.Text;
        DialogResult = true;
    }

    private void OnUnlink(object sender, RoutedEventArgs e)
    {
        Unlink = true;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
