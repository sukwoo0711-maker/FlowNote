using System.IO;
using System.Windows;
using System.Windows.Threading;
using FlowNote.Desktop.Views;

namespace FlowNote.Desktop.Capture;

public sealed class GdiRegionCaptureService : IRegionCaptureService
{
    public async Task<RegionCaptureResult?> CaptureAsync(Window? owner)
    {
        var overlay = new RegionCaptureOverlay();
        var tcs = new TaskCompletionSource<Rect?>();
        overlay.SelectionCompleted += rect => tcs.TrySetResult(rect);
        overlay.Canceled += () => tcs.TrySetResult(null);
        overlay.Show();
        overlay.Activate();
        _ = owner;
        var selection = await tcs.Task.ConfigureAwait(true);
        overlay.Hide();
        overlay.Close();
        await Dispatcher.Yield(DispatcherPriority.Render);
        await Task.Delay(40);
        if (selection is null || selection.Value.Width < 2 || selection.Value.Height < 2)
        {
            return null;
        }

        var rect = selection.Value;
        var x = (int)Math.Round(rect.X);
        var y = (int)Math.Round(rect.Y);
        var width = Math.Max(1, (int)Math.Round(rect.Width));
        var height = Math.Max(1, (int)Math.Round(rect.Height));
        var stagingDir = Path.Combine(Path.GetTempPath(), "FlowNoteCapture");
        Directory.CreateDirectory(stagingDir);
        var path = Path.Combine(stagingDir, Guid.NewGuid().ToString("N") + ".png");
        using (var bitmap = new System.Drawing.Bitmap(width, height))
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        return new RegionCaptureResult(path, width, height);
    }
}
