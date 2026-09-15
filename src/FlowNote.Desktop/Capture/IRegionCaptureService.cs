using System.Windows;

namespace FlowNote.Desktop.Capture;

public sealed record RegionCaptureResult(string StagingPath, int WidthPx, int HeightPx);

public interface IRegionCaptureService
{
    Task<RegionCaptureResult?> CaptureAsync(Window? owner);
}
