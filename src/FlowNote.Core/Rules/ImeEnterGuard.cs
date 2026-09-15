namespace FlowNote.Core.Rules;

/// <summary>
/// Distinguishes IME composition-confirm Enter from a save command.
/// Call <see cref="ClearConfirmPending"/> after the current input dispatcher cycle
/// (not on a fixed millisecond delay) so a later Enter can save.
/// </summary>
public sealed class ImeEnterGuard
{
    public bool IsComposing { get; private set; }

    public bool ConfirmPending { get; private set; }

    public void OnCompositionStart()
    {
        IsComposing = true;
        ConfirmPending = false;
    }

    public void OnCompositionUpdate()
    {
        IsComposing = true;
    }

    public void OnCompositionEnd()
    {
        IsComposing = false;
        ConfirmPending = true;
    }

    public void ClearConfirmPending() => ConfirmPending = false;

    public bool ShouldSaveOnEnter(bool isImeProcessedKey)
    {
        if (IsComposing || isImeProcessedKey)
        {
            return false;
        }

        if (ConfirmPending)
        {
            ConfirmPending = false;
            return false;
        }

        return true;
    }
}
