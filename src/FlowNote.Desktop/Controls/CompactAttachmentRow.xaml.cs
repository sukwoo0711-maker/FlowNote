using System.IO;
using System.Windows;
using System.Windows.Controls;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Desktop.Controls;

public partial class CompactAttachmentRow : UserControl
{
    public static readonly RoutedEvent RemoveRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(RemoveRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(CompactAttachmentRow));

    public CompactAttachmentRow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind();
        Loaded += (_, _) => Bind();
    }

    public event RoutedEventHandler RemoveRequested
    {
        add => AddHandler(RemoveRequestedEvent, value);
        remove => RemoveHandler(RemoveRequestedEvent, value);
    }

    private void OnRemove(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(RemoveRequestedEvent, this));

    private void Bind()
    {
        if (Thumb is null || DataContext is not PendingAttachment pending)
        {
            return;
        }

        ImagePreview.SetPath(Thumb, AttachmentRules.IsImage(pending.MediaType) ? pending.SourcePath : null);
        NameText.Text = pending.OriginalName;
        var size = pending.ByteSize > 0 ? pending.ByteSize : FileLength(pending.SourcePath);
        MetaText.Text = FileSizeDisplay.Format(size);
        FailText.Visibility = Visibility.Collapsed;
        var image = AttachmentRules.IsImage(pending.MediaType);
        FileGlyph.Visibility = image ? Visibility.Collapsed : Visibility.Visible;
        Thumb.Visibility = Visibility.Collapsed;
        if (!image)
        {
            return;
        }

        var bitmap = CapsuleImageLoader.TryLoad(pending.SourcePath, 96);
        if (bitmap is null)
        {
            FailText.Text = "이미지를 열 수 없음";
            FailText.Visibility = Visibility.Visible;
            FileGlyph.Visibility = Visibility.Collapsed;
            return;
        }

        Thumb.Source = bitmap;
        Thumb.Visibility = Visibility.Visible;
        FileGlyph.Visibility = Visibility.Collapsed;
    }

    private static long FileLength(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
