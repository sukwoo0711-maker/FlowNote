using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using FlowNote.Core.Clipboard;

namespace FlowNote.Desktop.Paste;

public static class ClipboardSnapshot
{
    public static bool IsPasteGesture(System.Windows.Input.KeyEventArgs args)
    {
        return (args.Key == System.Windows.Input.Key.V
                && args.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
               || (args.Key == System.Windows.Input.Key.Insert
                   && args.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Shift);
    }

    public static ClipboardImportPlan? TryPlan(IDataObject? data, string fileStamp)
    {
        var offer = TryRead(data);
        return offer is null ? null : ClipboardImportPlanner.Plan(offer, fileStamp);
    }

    public static ClipboardOffer? TryRead(IDataObject? data)
    {
        if (data is null)
        {
            return null;
        }

        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                return Read(data);
            }
            catch (COMException)
            {
                System.Threading.Thread.Sleep(20);
            }
            catch (OutOfMemoryException)
            {
                return null;
            }
        }

        return null;
    }

    private static ClipboardOffer Read(IDataObject data)
    {
        var formats = SafeFormats(data);
        var files = ReadFileDrop(data);
        var xml = ReadText(data, formats, "XML Spreadsheet");
        var html = ReadText(data, formats, "HTML Format", "text/html", DataFormats.Html);
        var rtf = ReadText(data, formats, DataFormats.Rtf, "text/rtf");
        var csv = ReadText(data, formats, "Csv", "CSV", "text/csv");
        var text = ReadText(data, formats, DataFormats.UnicodeText, "text/plain", DataFormats.Text);
        var png = ReadBytes(data, formats, "PNG", "image/png")
                  ?? EncodeBitmap(data, formats, autoConvert: false);
        if (png is null && files.Count == 0 && xml is null && !LooksRich(html, rtf))
        {
            png = EncodeBitmap(data, formats, autoConvert: true);
        }

        return new ClipboardOffer
        {
            FilePaths = files,
            Biff12 = ReadBytes(data, formats, "Biff12"),
            Biff8 = ReadBytes(data, formats, "Biff8", "Biff5"),
            XmlSpreadsheet = xml,
            Html = html,
            Rtf = rtf,
            Csv = csv,
            Text = text,
            Png = png
        };
    }

    private static bool LooksRich(string? html, string? rtf)
        => (!string.IsNullOrWhiteSpace(html) && CfHtml.HasTable(html))
           || (!string.IsNullOrWhiteSpace(rtf) && (rtf.Contains(@"\trowd", StringComparison.Ordinal) || rtf.Contains(@"\pict", StringComparison.Ordinal)));

    private static IReadOnlyList<string> ReadFileDrop(IDataObject data)
    {
        try
        {
            if (!data.GetDataPresent(DataFormats.FileDrop, false))
            {
                return [];
            }

            return data.GetData(DataFormats.FileDrop, false) is string[] paths
                ? paths.Where(static path => !string.IsNullOrWhiteSpace(path)).ToArray()
                : [];
        }
        catch
        {
            return [];
        }
    }

    private static string? ReadText(IDataObject data, IReadOnlyList<string> formats, params string[] names)
    {
        var format = Find(formats, names);
        if (format is null)
        {
            return null;
        }

        try
        {
            var raw = data.GetData(format, false);
            return raw switch
            {
                string text => text,
                byte[] bytes => DecodeText(bytes),
                MemoryStream stream => DecodeText(stream.ToArray()),
                Stream stream => DecodeText(ReadStream(stream)),
                _ => raw?.ToString()
            };
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? ReadBytes(IDataObject data, IReadOnlyList<string> formats, params string[] names)
    {
        var format = Find(formats, names);
        if (format is null)
        {
            return null;
        }

        try
        {
            var raw = data.GetData(format, false);
            return raw switch
            {
                byte[] bytes => bytes.Length == 0 ? null : bytes,
                MemoryStream stream => NonEmpty(stream.ToArray()),
                Stream stream => NonEmpty(ReadStream(stream)),
                string text => string.IsNullOrEmpty(text) ? null : Encoding.UTF8.GetBytes(text),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? EncodeBitmap(IDataObject data, IReadOnlyList<string> formats, bool autoConvert)
    {
        try
        {
            if (!autoConvert && Find(formats, DataFormats.Bitmap, "Bitmap", DataFormats.Dib, "DeviceIndependentBitmap") is null)
            {
                return null;
            }

            if (!data.GetDataPresent(DataFormats.Bitmap, autoConvert))
            {
                return null;
            }

            if (data.GetData(DataFormats.Bitmap, autoConvert) is not BitmapSource bitmap)
            {
                return null;
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return NonEmpty(stream.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> SafeFormats(IDataObject data)
    {
        try
        {
            return data.GetFormats(false) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? Find(IReadOnlyList<string> formats, params string[] names)
    {
        foreach (var name in names)
        {
            var hit = formats.FirstOrDefault(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    private static byte[] ReadStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static byte[]? NonEmpty(byte[] bytes) => bytes.Length == 0 ? null : bytes;

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
