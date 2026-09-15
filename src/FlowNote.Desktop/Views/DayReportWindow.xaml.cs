using System.Windows;
using FlowNote.Desktop.Theming;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Views;

public partial class DayReportWindow : Window
{
    public DayReportWindow(DayReportViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        GlassChrome.Attach(this);
    }

    private void OnCaptionClose(object sender, RoutedEventArgs e) => Close();
}
