using System.Windows;

namespace FlowNote.Desktop.Preview;

public partial class ComponentPreviewWindow : Window
{
    public ComponentPreviewWindow()
    {
        InitializeComponent();
        DataContext = new ComponentPreviewViewModel();
    }

    public FrameworkElement CaptureRoot => PreviewRoot;
}

internal sealed class ComponentPreviewViewModel
{
    public PreviewComposerState EmptyComposer { get; } = PreviewFixtures.EmptyComposer();

    public PreviewComposerState InputComposer { get; } = PreviewFixtures.InputComposer();

    public PreviewComposerState AttachmentComposer { get; } = PreviewFixtures.AttachmentComposer();

    public PreviewComposerState ErrorComposer { get; } = PreviewFixtures.ErrorComposer();

    public object WorkItems { get; } = PreviewFixtures.WorkItems();

    public object EntryCards { get; } = PreviewFixtures.EntryCards();

    public object EventRows { get; } = PreviewFixtures.EventRows();
}
