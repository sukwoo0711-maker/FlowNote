using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Controls;

public partial class AttachmentTile : UserControl
{
    public static readonly RoutedEvent RemoveRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(RemoveRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(AttachmentTile));

    public AttachmentTile()
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

    private void OnRemoveClick(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(RemoveRequestedEvent, this));

    private void Bind()
    {
        if (Thumb is null)
        {
            return;
        }

        string name;
        string path;
        string media;
        long size;
        var canRemove = false;
        switch (DataContext)
        {
            case PendingAttachment pending:
                name = pending.OriginalName;
                path = pending.SourcePath;
                media = pending.MediaType;
                size = pending.ByteSize > 0 ? pending.ByteSize : FileLength(pending.SourcePath);
                canRemove = true;
                break;
            case AttachmentPreview preview:
                name = preview.Name;
                path = preview.Path ?? "";
                media = preview.IsImage ? "image/png" : "application/octet-stream";
                size = preview.ByteSize;
                break;
            default:
                return;
        }

        NameText.Text = name;
        MetaText.Text = FileSizeDisplay.Format(size);
        RemoveButton.Visibility = canRemove ? Visibility.Visible : Visibility.Collapsed;
        var image = AttachmentRules.IsImage(media);
        FileGlyph.Visibility = image ? Visibility.Collapsed : Visibility.Visible;
        Thumb.Visibility = Visibility.Collapsed;
        FailText.Visibility = Visibility.Collapsed;
        if (!image)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            FailText.Text = "이미지를 열 수 없음";
            FailText.Visibility = Visibility.Visible;
            FileGlyph.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 72;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            Thumb.Source = bitmap;
            Thumb.Visibility = Visibility.Visible;
            FileGlyph.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            Thumb.Visibility = Visibility.Collapsed;
            FailText.Text = "이미지를 열 수 없음";
            FailText.Visibility = Visibility.Visible;
            FileGlyph.Visibility = Visibility.Collapsed;
        }
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
