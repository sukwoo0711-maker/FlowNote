using System.Drawing;
using System.Windows;

namespace FlowNote.Desktop.Theming;

internal static class BrandIcon
{
    public static Icon LoadTrayIcon()
    {
        var ico = TryLoadIcon(new Uri("pack://application:,,,/Assets/FlowNote.ico"));
        if (ico is not null)
        {
            return ico;
        }

        var png = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/FlowNote.png"))?.Stream;
        if (png is not null)
        {
            using var bitmap = new Bitmap(png);
            using var handleIcon = Icon.FromHandle(bitmap.GetHicon());
            return (Icon)handleIcon.Clone();
        }

        return SystemIcons.Application;
    }

    private static Icon? TryLoadIcon(Uri uri)
    {
        try
        {
            var stream = Application.GetResourceStream(uri)?.Stream;
            return stream is null ? null : new Icon(stream);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (System.IO.FileFormatException)
        {
            return null;
        }
    }
}
