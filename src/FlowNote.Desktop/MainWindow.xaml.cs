using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel, AppSession session)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        _ = session;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) =>
        {
            ApplyDetailLayout();
            ApplyViewModeButtons();
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        if (Width > workArea.Width)
        {
            Width = workArea.Width;
        }

        if (Height > workArea.Height)
        {
            Height = workArea.Height;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.SetNarrow(ActualWidth < 960);
        _viewModel.SetNavRail(ActualWidth < 1000);
        ApplyDetailLayout();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.HasDetail)
            or nameof(MainViewModel.ShowDetailColumn)
            or nameof(MainViewModel.IsNarrow)
            or nameof(MainViewModel.IsNavRail))
        {
            ApplyDetailLayout();
        }

        if (e.PropertyName is nameof(MainViewModel.HasDetail) && !_viewModel.HasDetail)
        {
            TimelineList.SelectedItem = null;
        }

        if (e.PropertyName is nameof(MainViewModel.IsChronological)
            or nameof(MainViewModel.IsByWork)
            or nameof(MainViewModel.IsPanorama)
            or nameof(MainViewModel.Session))
        {
            ApplyViewModeButtons();
        }
    }

    private void OnTimelineSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.Select(TimelineList.SelectedItem as TimelineRow);
    }

    private void OnViewModeClick(object sender, RoutedEventArgs e) => ApplyViewModeButtons();

    private void ApplyDetailLayout()
    {
        if (DetailColumn is null)
        {
            return;
        }

        if (NavColumn is not null)
        {
            NavColumn.Width = _viewModel.IsNavRail ? new GridLength(56) : new GridLength(168);
        }

        DetailColumn.Width = _viewModel.ShowDetailColumn ? new GridLength(352) : new GridLength(0);
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape)
        {
            return;
        }

        if (_viewModel.ShowNextActionEditor)
        {
            _viewModel.CancelNextActionCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (_viewModel.ShowContinuePopup)
        {
            _viewModel.ShowContinuePopup = false;
            e.Handled = true;
            return;
        }

        if (_viewModel.HasDetail)
        {
            _viewModel.CloseDetail();
            e.Handled = true;
        }
    }

    private void ApplyViewModeButtons()
    {
        if (ChronologicalButton is null)
        {
            return;
        }

        ApplyToggle(ChronologicalButton, _viewModel.IsChronological);
        ApplyToggle(PanoramaButton, _viewModel.IsPanorama);
        ApplyToggle(ByWorkButton, _viewModel.IsByWork);
    }

    private void ApplyToggle(Button button, bool on)
    {
        button.Style = on
            ? (Style)FindResource("ViewModeButtonOn")
            : (Style)FindResource("ViewModeButton");
    }
}
