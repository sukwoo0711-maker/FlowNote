namespace FlowNote.Core.Rules;

public static class CapsuleLayout
{
    public const int LayoutVersion = 2;
    public const double SurfaceWidth = 520;
    public const double SurfaceHeight = 52;
    public const double CornerRadius = 26;
    public const double Gutter = 8;
    public const double PanelGap = 6;
    public const double PanelWidthInset = 40;
    public const double PanelCornerRadius = 14;
    public const double PreviewRowHeight = 28;
    public const double MaxPreviewVisibleHeight = 240;
    public const double MaxEditVisibleHeight = 320;
    public const int PreviewRowCount = 3;

    public static double HostWidth => SurfaceWidth + (Gutter * 2);

    public static double HostHeightWithoutPanel => SurfaceHeight + (Gutter * 2);

    public static double PanelWidth => SurfaceWidth - PanelWidthInset;

    public static double HostHeightWithPanel(double panelHeight)
        => SurfaceHeight + (Gutter * 2) + PanelGap + panelHeight;

    public static bool ShouldOpenPanelAbove(double capsuleBottom, double panelHeight, double workBottom)
        => capsuleBottom + PanelGap + panelHeight + Gutter > workBottom;
}

public enum CapsulePanelKind
{
    None,
    Recent,
    Todo,
    Draft,
    Menu
}

public enum CapsuleInputMode
{
    SingleLine,
    Multiline
}

public enum FloatingPinnedPreview
{
    None,
    Todo,
    Recent
}
