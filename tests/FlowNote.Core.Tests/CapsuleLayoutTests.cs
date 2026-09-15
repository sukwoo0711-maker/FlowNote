using FlowNote.Core.Rules;

namespace FlowNote.Core.Tests;

public sealed class CapsuleLayoutTests
{
    [Fact]
    public void Host_without_panel_is_gutter_around_520x52()
    {
        Assert.Equal(536, CapsuleLayout.HostWidth);
        Assert.Equal(68, CapsuleLayout.HostHeightWithoutPanel);
        Assert.Equal(26, CapsuleLayout.CornerRadius);
        Assert.Equal(480, CapsuleLayout.PanelWidth);
    }

    [Fact]
    public void Host_with_panel_includes_gap_and_does_not_keep_hidden_height()
    {
        Assert.Equal(68, CapsuleLayout.HostHeightWithoutPanel);
        Assert.Equal(174, CapsuleLayout.HostHeightWithPanel(100));
    }

    [Fact]
    public void Panel_opens_above_when_not_enough_room_below()
    {
        Assert.False(CapsuleLayout.ShouldOpenPanelAbove(capsuleBottom: 100, panelHeight: 80, workBottom: 400));
        Assert.True(CapsuleLayout.ShouldOpenPanelAbove(capsuleBottom: 380, panelHeight: 80, workBottom: 400));
    }
}
