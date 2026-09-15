using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Desktop.Paste;
using FlowNote.Desktop.ViewModels;
using FlowNote.Desktop.Views;

namespace FlowNote.Desktop.Controls;

public partial class FloatingAuxiliaryPanel : UserControl
{
    private readonly ImeEnterGuard _todoIme = new();
    private readonly ImeEnterGuard _longIme = new();

    public FloatingAuxiliaryPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (TodoTitle is not null)
            {
                TextCompositionManager.AddPreviewTextInputStartHandler(TodoTitle, (_, _) => _todoIme.OnCompositionStart());
                TextCompositionManager.AddPreviewTextInputUpdateHandler(TodoTitle, (_, _) => _todoIme.OnCompositionUpdate());
                TextCompositionManager.AddPreviewTextInputHandler(TodoTitle, (_, _) =>
                {
                    _todoIme.OnCompositionEnd();
                    Dispatcher.BeginInvoke(_todoIme.ClearConfirmPending, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                });
            }

            if (LongMemo is not null)
            {
                TextCompositionManager.AddPreviewTextInputStartHandler(LongMemo, (_, _) => _longIme.OnCompositionStart());
                TextCompositionManager.AddPreviewTextInputUpdateHandler(LongMemo, (_, _) => _longIme.OnCompositionUpdate());
                TextCompositionManager.AddPreviewTextInputHandler(LongMemo, (_, _) =>
                {
                    _longIme.OnCompositionEnd();
                    Dispatcher.BeginInvoke(_longIme.ClearConfirmPending, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                });
            }
        };
    }

    private FloatingViewModel? Vm => DataContext as FloatingViewModel;

    private void OnRecentOpen(object sender, RoutedEventArgs e)
    {
        if (sender is CompactLogRow { DataContext: CapsuleRecentRow row })
        {
            Vm?.OpenRecentEntry(row);
        }
    }

    private void OnTodoOpen(object sender, RoutedEventArgs e)
    {
        if (sender is CompactTodoRow { DataContext: OpenWorkItemRow row })
        {
            Vm?.OpenWorkFromTodo(row);
        }
    }

    private async void OnTodoComplete(object sender, RoutedEventArgs e)
    {
        if (sender is CompactTodoRow { DataContext: OpenWorkItemRow row } && Vm is not null)
        {
            await Vm.CompleteAsync(row);
        }
    }

    private void OnRemoveAttachment(object sender, RoutedEventArgs e)
    {
        if (sender is CompactAttachmentRow { DataContext: PendingAttachment pending })
        {
            Vm?.RemovePending(pending);
        }
    }

    private void OnTodoKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter and not Key.ImeProcessed)
        {
            return;
        }

        if (!_todoIme.ShouldSaveOnEnter(e.Key == Key.ImeProcessed) || e.Key == Key.ImeProcessed)
        {
            return;
        }

        e.Handled = true;
        if (Vm?.AddWorkItemCommand.CanExecute(null) == true)
        {
            Vm.AddWorkItemCommand.Execute(null);
        }
    }

    private void OnLongMemoPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (ClipboardSnapshot.TryPlan(e.DataObject, DateTime.Now.ToString("HHmmss"))?.ConsumesPaste != true)
        {
            return;
        }

        e.CancelCommand();
        if (Window.GetWindow(this) is FloatingCapsuleWindow window)
        {
            window.TryImportPaste(e.DataObject);
        }
    }

    private void OnLongMemoKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (!_longIme.ShouldSaveOnEnter(false))
            {
                return;
            }

            e.Handled = true;
            if (Vm?.SaveNoteCommand.CanExecute(null) == true)
            {
                Vm.SaveNoteCommand.Execute(null);
            }
        }
    }
}
