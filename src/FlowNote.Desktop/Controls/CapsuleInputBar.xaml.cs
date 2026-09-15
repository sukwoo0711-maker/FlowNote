using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlowNote.Core.Rules;
using FlowNote.Desktop.Paste;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Controls;

public partial class CapsuleInputBar : UserControl
{
    private readonly ImeEnterGuard _ime = new();
    private Point _logoDown;
    private bool _logoDragging;

    public CapsuleInputBar()
    {
        InitializeComponent();
        TextCompositionManager.AddPreviewTextInputStartHandler(Memo, (_, _) => _ime.OnCompositionStart());
        TextCompositionManager.AddPreviewTextInputUpdateHandler(Memo, (_, _) => _ime.OnCompositionUpdate());
        TextCompositionManager.AddPreviewTextInputHandler(Memo, (_, _) =>
        {
            _ime.OnCompositionEnd();
            Dispatcher.BeginInvoke(_ime.ClearConfirmPending, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        });
    }

    public TextBox MemoBox => Memo;

    public event EventHandler? LogoMenuRequested;

    public event EventHandler<IDataObject>? RichPasteRequested;

    private FloatingViewModel? Vm => DataContext as FloatingViewModel;

    private void OnLogoDown(object sender, MouseButtonEventArgs e)
    {
        _logoDown = e.GetPosition(this);
        _logoDragging = false;
    }

    private void OnLogoMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _logoDragging)
        {
            return;
        }

        var delta = e.GetPosition(this) - _logoDown;
        if (Math.Abs(delta.X) > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(delta.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _logoDragging = true;
            Window.GetWindow(this)?.DragMove();
        }
    }

    private void OnLogoClick(object sender, RoutedEventArgs e)
    {
        if (_logoDragging)
        {
            e.Handled = true;
            return;
        }

        LogoMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMemoPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = Vm;
        if (vm is null)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            return;
        }

        if (ClipboardSnapshot.IsPasteGesture(e))
        {
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            e.Handled = true;
            if (!_ime.IsComposing)
            {
                vm.OpenLongNote();
            }

            return;
        }

        if (e.Key is not Key.Enter and not Key.ImeProcessed)
        {
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers is not ModifierKeys.None and not ModifierKeys.Control)
        {
            return;
        }

        if (!_ime.ShouldSaveOnEnter(e.Key == Key.ImeProcessed))
        {
            return;
        }

        if (e.Key == Key.ImeProcessed)
        {
            return;
        }

        e.Handled = true;
        if (vm.SaveNoteCommand.CanExecute(null))
        {
            vm.SaveNoteCommand.Execute(null);
        }
    }

    private void OnMemoTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ime.IsComposing)
        {
            return;
        }

        Vm?.NotifyPossibleMultilinePaste();
    }

    private void OnMemoPasting(object sender, DataObjectPastingEventArgs e)
    {
        var plan = ClipboardSnapshot.TryPlan(e.DataObject, DateTime.Now.ToString("HHmmss"));
        if (plan?.ConsumesPaste == true)
        {
            e.CancelCommand();
            RichPasteRequested?.Invoke(this, e.DataObject);
            return;
        }

        if (e.DataObject.GetDataPresent(DataFormats.UnicodeText)
            && e.DataObject.GetData(DataFormats.UnicodeText) is string text
            && (text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal)))
        {
            Dispatcher.BeginInvoke(() => Vm?.NotifyPossibleMultilinePaste(), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
