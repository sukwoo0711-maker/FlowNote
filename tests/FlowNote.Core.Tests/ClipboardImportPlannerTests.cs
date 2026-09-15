using System.Text;
using FlowNote.Core.Clipboard;

namespace FlowNote.Core.Tests;

public sealed class ClipboardImportPlannerTests
{
    [Fact]
    public void Excel_keeps_spreadsheet_xml_not_tab_text_or_bitmap()
    {
        var plan = ClipboardImportPlanner.Plan(new ClipboardOffer
        {
            XmlSpreadsheet = """
                <?xml version="1.0"?>
                <Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet">
                  <Worksheet ss:Name="Sheet1"/>
                </Workbook>
                """,
            Html = "<table><tr><td>A</td><td>B</td></tr></table>",
            Text = "A\tB\n1\t2",
            Png = [137, 80, 78, 71]
        }, "120000");

        var part = Assert.Single(plan.Attachments);
        Assert.Equal("paste-120000.xml", part.FileName);
        Assert.Null(plan.InsertText);
        Assert.True(plan.ConsumesPaste);
    }

    [Fact]
    public void Html_table_is_kept_when_spreadsheet_is_missing()
    {
        var plan = ClipboardImportPlanner.Plan(new ClipboardOffer
        {
            Html = "Version:0.9\r\nStartHTML:0000000000\r\n<!--StartFragment--><table><tr><td>단가</td></tr></table><!--EndFragment-->",
            Text = "단가\n100"
        }, "120001");

        var part = Assert.Single(plan.Attachments);
        Assert.EndsWith(".html", part.FileName, StringComparison.Ordinal);
        var html = Encoding.UTF8.GetString(part.Bytes);
        Assert.Contains("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.Null(plan.InsertText);
    }

    [Fact]
    public void Image_only_clipboard_becomes_png()
    {
        var plan = ClipboardImportPlanner.Plan(new ClipboardOffer
        {
            Png = [1, 2, 3, 4]
        }, "120002");

        var part = Assert.Single(plan.Attachments);
        Assert.Equal("paste-120002.png", part.FileName);
        Assert.Equal("image/png", part.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, part.Bytes);
    }

    [Fact]
    public void Browser_image_prefers_png_over_img_html()
    {
        var plan = ClipboardImportPlanner.Plan(new ClipboardOffer
        {
            Html = "<html><body><img src=\"https://example.com/a.png\"></body></html>",
            Png = [9, 8, 7]
        }, "120003");

        Assert.Equal("paste-120003.png", Assert.Single(plan.Attachments).FileName);
    }

    [Fact]
    public void Plain_text_stays_in_the_composer()
    {
        var plan = ClipboardImportPlanner.Plan(new ClipboardOffer
        {
            Text = "한 줄 메모",
            Html = "<html><body><!--StartFragment-->한 줄 메모<!--EndFragment--></body></html>"
        }, "120004");

        Assert.Empty(plan.Attachments);
        Assert.Equal("한 줄 메모", plan.InsertText);
        Assert.False(plan.ConsumesPaste);
    }

    [Fact]
    public void Tab_table_text_is_saved_as_tsv()
    {
        var plan = ClipboardImportPlanner.Plan(new ClipboardOffer
        {
            Text = "이름\t수량\n나사\t12"
        }, "120005");

        var part = Assert.Single(plan.Attachments);
        Assert.Equal("paste-120005.tsv", part.FileName);
        Assert.Null(plan.InsertText);
    }

    [Fact]
    public void File_drop_wins_and_keeps_short_caption()
    {
        var path = Path.GetTempFileName();
        try
        {
            var plan = ClipboardImportPlanner.Plan(new ClipboardOffer
            {
                FilePaths = [path],
                Text = "캡션",
                Png = [1]
            }, "120006");

            Assert.Equal(path, Assert.Single(plan.ExistingFilePaths));
            Assert.Empty(plan.Attachments);
            Assert.Equal("캡션", plan.InsertText);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rtf_table_is_kept()
    {
        var plan = ClipboardImportPlanner.Plan(new ClipboardOffer
        {
            Rtf = @"{\rtf1\ansi\trowd\cell 값\cell}",
            Text = "값"
        }, "120007");

        Assert.Equal("paste-120007.rtf", Assert.Single(plan.Attachments).FileName);
    }

    [Fact]
    public void CfHtml_extracts_fragment()
    {
        var fragment = CfHtml.ExtractFragment(
            "Version:0.9\r\nStartHTML:0000000000\r\n<!--StartFragment--><b>굵게</b><!--EndFragment-->");
        Assert.Equal("<b>굵게</b>", fragment);
    }
}
