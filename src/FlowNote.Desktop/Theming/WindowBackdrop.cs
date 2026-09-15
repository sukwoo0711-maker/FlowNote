using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace FlowNote.Desktop.Theming;

internal static class WindowBackdrop
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmsbtAcrylic = 3;
    private const int DwmwcpRound = 2;
    private const int DwmwaColorNone = unchecked((int)0xFFFFFFFE);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void TryApplyMica(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || SystemParameters.HighContrast)
        {
            window.Background = new SolidColorBrush(Color.FromRgb(0xED, 0xE8, 0xE1));
            return;
        }

        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        var dark = 0;
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        var round = DwmwcpRound;
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaWindowCornerPreference, ref round, sizeof(int));
        var none = DwmwaColorNone;
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaCaptionColor, ref none, sizeof(int));
        var hairline = 0x0096B8D4;
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaBorderColor, ref hairline, sizeof(int));
        var acrylic = DwmsbtAcrylic;
        if (DwmSetWindowAttribute(helper.Handle, DwmwaSystemBackdropType, ref acrylic, sizeof(int)) == 0)
        {
            window.Background = new SolidColorBrush(Color.FromArgb(0x59, 0xED, 0xE8, 0xE1));
        }
    }
}
