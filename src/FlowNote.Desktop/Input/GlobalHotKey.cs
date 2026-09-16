using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FlowNote.Desktop.Input;

internal sealed class GlobalHotKey : IDisposable
{
    public const int CaptureId = 0x464E;

    private HwndSource? _source;
    private readonly Action _onCapture;

    public GlobalHotKey(Action onCapture)
    {
        _onCapture = onCapture;
        var parameters = new HwndSourceParameters("FlowNote.HotKey")
        {
            Width = 1,
            Height = 1,
            PositionX = -32000,
            PositionY = -32000,
            WindowStyle = unchecked((int)0x80000000)
        };
        _source = new HwndSource(parameters);
        _source.AddHook(Hook);
        CaptureRegistered = Native.RegisterHotKey(_source.Handle, CaptureId,
            Native.ModControl | Native.ModAlt | Native.ModNoRepeat, Native.VkSpace);
        if (!CaptureRegistered)
        {
            PrimaryError = Marshal.GetLastWin32Error();
            CaptureRegistered = Native.RegisterHotKey(_source.Handle, CaptureId,
                Native.ModControl | Native.ModAlt | Native.ModNoRepeat, 0x4E);
            if (CaptureRegistered) ActiveLabel = "Ctrl+Alt+N";
            else RegistrationError = Marshal.GetLastWin32Error();
        }
    }

    public bool CaptureRegistered { get; }
    public string ActiveLabel { get; } = CaptureLabel;
    public int PrimaryError { get; }
    public int RegistrationError { get; }
    public bool UsesFallback => CaptureRegistered && ActiveLabel != CaptureLabel;

    public static string CaptureLabel => "Ctrl+Alt+Space";

    public void Dispose()
    {
        if (_source is null)
        {
            return;
        }

        if (CaptureRegistered) Native.UnregisterHotKey(_source.Handle, CaptureId);
        _source.RemoveHook(Hook);
        _source.Dispose();
        _source = null;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WmHotkey && wParam.ToInt32() == CaptureId)
        {
            _onCapture();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static class Native
    {
        public const int WmHotkey = 0x0312;
        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModNoRepeat = 0x4000;
        public const uint VkSpace = 0x20;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr handle, int id);
    }
}
