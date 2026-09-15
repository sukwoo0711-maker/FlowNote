using System.Windows;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Views;

public partial class DayReportWindow : Window
{
    public DayReportWindow(DayReportViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
