using System.Windows;
using System.Windows.Shell;

namespace FlowNote.Desktop.Theming;

internal static class GlassChrome
{
    public const int CaptionHeight = 48;

    public static void Attach(Window window)
    {
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = CaptionHeight,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(-1),
            UseAeroCaptionButtons = false,
            CornerRadius = new CornerRadius(0)
        });
        WindowBackdrop.TryApplyMica(window);
    }
}
